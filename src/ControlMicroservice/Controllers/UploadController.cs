namespace ControlMicroservice.Controllers;

using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using System.Globalization;
using ControlMicroservice.DTOs;
using ControlMicroservice.Services;

[Authorize] // Requiere estar autenticado con el token del Microservicio 0
[ApiController]
[Route("api/control")]
[Consumes("multipart/form-data")]
public class UploadController : ControllerBase
{

   // Estados que representan una carga ya finalizada/aceptada para el periodo (bloquean con rechazo)
   private static readonly HashSet<string> EstadosRechazo = new(StringComparer.OrdinalIgnoreCase)
      { "Cargado", "Finalizado", "Notificado" };

   private readonly SeaweedFsService _seaweedService;
   private readonly RabbitMqPublisherService _rabbitMqPublisher;
   private readonly string _connectionString;

   public UploadController(
      SeaweedFsService seaweedService,
      RabbitMqPublisherService rabbitMqPublisher,
      IConfiguration configuration)
   {
      _seaweedService = seaweedService;
      _rabbitMqPublisher = rabbitMqPublisher;
      _connectionString = configuration.GetConnectionString("DefaultConnection")
         ?? throw new InvalidOperationException("No se configuró la conexión DefaultConnection.");
   }



   [HttpPost("upload")]
   [Consumes("multipart/form-data")]
   public async Task<IActionResult> SubirArchivo(IFormFile file)
   {
      if (file == null || file.Length == 0)
         return BadRequest(new { success = false, message = "No se ha adjuntado ningún archivo." });

      try
      {
         // 1. Leer temporalmente el Excel en memoria para extraer el periodo de la primera fila de datos
         string periodoExtraido = string.Empty;

         using (var stream = file.OpenReadStream())
         using (var workbook = new XLWorkbook(stream))
         {
            var worksheet = workbook.Worksheet(1);
            var filasUsadas = worksheet.RowsUsed().ToList();
            var filaCabecera = filasUsadas.FirstOrDefault(fila =>
               fila.CellsUsed().Any(celda =>
                  EsColumnaPeriodo(celda.GetString())));

            if (filaCabecera == null)
               return BadRequest(new { success = false, message = "El archivo Excel no contiene una columna 'Periodo' o 'Fecha'." });

            var celdaCabeceraPeriodo = filaCabecera.CellsUsed().First(celda =>
               EsColumnaPeriodo(celda.GetString()));
            var primeraFilaDatos = filasUsadas.FirstOrDefault(fila => fila.RowNumber() > filaCabecera.RowNumber()
               && !string.IsNullOrWhiteSpace(fila.Cell(celdaCabeceraPeriodo.Address.ColumnNumber).GetString()));

            if (primeraFilaDatos == null)
               return BadRequest(new { success = false, message = "El archivo Excel está vacío o no contiene registros." });

            var celdaPeriodo = primeraFilaDatos.Cell(celdaCabeceraPeriodo.Address.ColumnNumber);
            var valorPeriodo = celdaPeriodo.GetString().Trim();

            if (DateTime.TryParseExact(
                  valorPeriodo,
                  new[] { "yyyy-MM", "yyyy/MM", "MM/yyyy" },
                  CultureInfo.InvariantCulture,
                  DateTimeStyles.AllowWhiteSpaces,
                  out var fechaParsed)
               || celdaPeriodo.TryGetValue<DateTime>(out fechaParsed)
               || (celdaPeriodo.TryGetValue<double>(out var numeroFecha)
                  && DateTime.TryParse(
                     DateTime.FromOADate(numeroFecha).ToString(CultureInfo.InvariantCulture),
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.AllowWhiteSpaces,
                     out fechaParsed))
               || DateTime.TryParse(
                  valorPeriodo,
                  CultureInfo.GetCultureInfo("es-PE"),
                  DateTimeStyles.AllowWhiteSpaces,
                  out fechaParsed)
               || DateTime.TryParse(
                  valorPeriodo,
                  CultureInfo.InvariantCulture,
                  DateTimeStyles.AllowWhiteSpaces,
                  out fechaParsed))
            {
               periodoExtraido = fechaParsed.ToString("yyyy-MM");
            }
            else
            {
               return BadRequest(new { success = false, message = "No se pudo determinar el periodo a partir de la fecha del registro." });
            }
         }

         string usuario = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("unique_name")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "UsuarioAnonimo";

         using var db = new SqlConnection(_connectionString);
         await db.OpenAsync();

         // 2. Validar duplicidad por periodo contra cargas previas registradas
         var cargaExistente = await db.QueryFirstOrDefaultAsync<CargaEstadoDto>(
            @"SELECT TOP 1 Id, Estado FROM CargaArchivo
              WHERE Periodo = @Periodo AND Estado IN ('Cargado', 'Finalizado', 'Notificado', 'Pendiente', 'En Proceso')
              ORDER BY CASE WHEN Estado IN ('Cargado', 'Finalizado', 'Notificado') THEN 0 ELSE 1 END, FechaRegistro DESC",
            new { Periodo = periodoExtraido });

         if (cargaExistente != null)
         {
            bool esRechazo = EstadosRechazo.Contains(cargaExistente.Estado);
            string resultadoAccion = esRechazo ? "RECHAZADO" : "BLOQUEADO";
            string motivo = esRechazo
               ? $"Ya existe una carga con estado '{cargaExistente.Estado}' para el periodo {periodoExtraido}. La carga es rechazada."
               : $"Ya existe una carga en curso (estado '{cargaExistente.Estado}') para el periodo {periodoExtraido}. No se permiten cargas simultáneas.";

            await db.ExecuteAsync(
               @"INSERT INTO CargaAuditoria (Periodo, NombreArchivo, Usuario, Resultado, Motivo, CargaArchivoIdReferencia, EstadoEncontrado, FechaIntento)
                 VALUES (@Periodo, @NombreArchivo, @Usuario, @Resultado, @Motivo, @CargaArchivoIdReferencia, @EstadoEncontrado, GETDATE())",
               new
               {
                  Periodo = periodoExtraido,
                  NombreArchivo = file.FileName,
                  Usuario = usuario,
                  Resultado = resultadoAccion,
                  Motivo = motivo,
                  CargaArchivoIdReferencia = cargaExistente.Id,
                  EstadoEncontrado = cargaExistente.Estado
               });

            return Conflict(new { success = false, resultado = resultadoAccion, message = motivo });
         }

         // 3. Subir el archivo original a SeaweedFS (solo si el periodo no tiene cargas activas o finalizadas)
         string rutaSeaweed = await _seaweedService.SubirArchivoAsync(file);

         // 4. Registrar la nueva carga con estado Pendiente
         var insertParams = new DynamicParameters();
         insertParams.Add("@NombreArchivo", file.FileName);
         insertParams.Add("@RutaArchivo", rutaSeaweed);
         insertParams.Add("@Usuario", usuario);
         insertParams.Add("@Periodo", periodoExtraido);
         insertParams.Add("@NuevoId", dbType: DbType.Int32, direction: ParameterDirection.Output);

         await db.ExecuteAsync("sp_RegistrarCargaArchivo", insertParams, commandType: CommandType.StoredProcedure);
         int idCarga = insertParams.Get<int>("@NuevoId");

         // 5. Publicar el mensaje en RabbitMQ para continuar el procesamiento
         string usuarioActual = User.Identity?.Name ?? "user@example.com";
         await _rabbitMqPublisher.PublicarMensaje(idCarga, rutaSeaweed, usuarioActual);

         return Ok(new
         {
            success = true,
            message = $"Archivo subido con éxito para el periodo detectado: {periodoExtraido}.",
            cargaArchivoId = idCarga,
            periodo = periodoExtraido,
            rutaArchivo = rutaSeaweed
         });
      }
      catch (Exception ex)
      {
         return StatusCode(500, new { success = false, message = ex.Message });
      }
   }

   [HttpGet("history")]
   public async Task<IActionResult> ObtenerHistorial([FromQuery] int limit = 100)
   {
      limit = Math.Clamp(limit, 1, 500);

      try
      {
         using var db = new SqlConnection(_connectionString);
         var historial = await db.QueryAsync<HistorialCargaDto>(
            @"SELECT TOP (@Limit)
                 Id,
                 NombreArchivo,
                 Periodo,
                 Usuario,
                 Estado,
                 FechaRegistro,
                 FechaFin
              FROM CargaArchivo
              ORDER BY FechaRegistro DESC, Id DESC",
            new { Limit = limit });

         return Ok(new
         {
            success = true,
            data = historial
         });
      }
      catch (Exception ex)
      {
         return StatusCode(500, new { success = false, message = ex.Message });
      }
   }

   private static bool EsColumnaPeriodo(string nombreColumna)
   {
      return string.Equals(nombreColumna.Trim(), "Periodo", StringComparison.OrdinalIgnoreCase)
         || string.Equals(nombreColumna.Trim(), "Fecha", StringComparison.OrdinalIgnoreCase);
   }

   private class CargaEstadoDto
   {
      public int Id { get; set; }
      public string Estado { get; set; } = string.Empty;
   }

   private sealed class HistorialCargaDto
   {
      public int Id { get; set; }
      public string NombreArchivo { get; set; } = string.Empty;
      public string Periodo { get; set; } = string.Empty;
      public string? Usuario { get; set; }
      public string Estado { get; set; } = string.Empty;
      public DateTime FechaRegistro { get; set; }
      public DateTime? FechaFin { get; set; }
   }

}