using ClosedXML.Excel;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BulkProcessMicroservice.Services;

public class ExcelProcessService
{
   private readonly string _connectionString;
   private readonly HttpClient _httpClient;
   private readonly string _seaweedBaseUrl;
   private readonly NotificationPublisherService _notificationPublisher;

   public ExcelProcessService(
      IConfiguration configuration,
      HttpClient httpClient,
      NotificationPublisherService notificationPublisher)
   {
      _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
      _httpClient = httpClient;
      _seaweedBaseUrl = configuration["SeaweedFS:BaseUrl"] ?? "http://localhost:8888";
      _notificationPublisher = notificationPublisher;
   }

   public async Task<ProcesamientoResultado> ProcesarArchivoExcelAsync(int cargaArchivoId, string rutaArchivo, string periodo)
   {
      using var db = new SqlConnection(_connectionString);
      await db.OpenAsync();
      await db.ExecuteAsync("UPDATE CargaArchivo SET Estado = 'En Proceso' WHERE Id = @Id", new { Id = cargaArchivoId });

      var nombreArchivo = await db.ExecuteScalarAsync<string>(
         "SELECT NombreArchivo FROM CargaArchivo WHERE Id = @Id", new { Id = cargaArchivoId }) ?? "N/A";

      // El archivo se resuelve después de registrar el inicio para que la trazabilidad refleje el consumo real.
      string? archivoParaProcesar = await ResolverRutaArchivoAsync(rutaArchivo);
      if (archivoParaProcesar is null) return ProcesamientoResultado.Fallido();

      int filasInsertadas = 0;
      int filasExistentes = 0;

      try
      {
         using var workbook = new XLWorkbook(archivoParaProcesar);
         var worksheet = workbook.Worksheet(1);
         var headerRow = worksheet.FirstRowUsed();
         if (headerRow is null)
         {
            await AuditarAsync(db, cargaArchivoId, periodo, nombreArchivo, "VALIDACION", "El archivo no contiene cabeceras.");
            return ProcesamientoResultado.Fallido();
         }

         var headers = headerRow.CellsUsed()
            .ToDictionary(cell => cell.GetString().Trim(), cell => cell.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
         if (!headers.TryGetValue("CodigoProducto", out var codigoColumn))
         {
            await AuditarAsync(db, cargaArchivoId, periodo, nombreArchivo, "VALIDACION", "El archivo no contiene la columna CodigoProducto.");
            return ProcesamientoResultado.Fallido();
         }

         foreach (var row in worksheet.RowsUsed().Where(row => row.RowNumber() > headerRow.RowNumber()))
         {
            if (row.CellsUsed().All(cell => string.IsNullOrWhiteSpace(cell.GetString())))
               continue;

            var codigoProducto = GetCellValue(row, headers, "CodigoProducto", defaultValue: "SIN_CODIGO");

            var existeRegistro = await db.ExecuteScalarAsync<int>(
               "SELECT COUNT(1) FROM DataProcesada WHERE CodigoProducto = @Codigo",
               new { Codigo = codigoProducto });
            if (existeRegistro > 0)
            {
               filasExistentes++;
               await AuditarAsync(db, cargaArchivoId, periodo, nombreArchivo, "EXISTENTE", $"El CodigoProducto '{codigoProducto}' ya existe.");
               continue;
            }

            var fecha = GetCellValue(row, headers, "Fecha", "FechaTransaccion");
            DateTime? fechaTransaccion = null;
            if (!string.IsNullOrWhiteSpace(fecha))
            {
               if (DateTime.TryParse(fecha, out var parsedDate))
                  fechaTransaccion = parsedDate;
               else
                  await AuditarAsync(db, cargaArchivoId, periodo, nombreArchivo, "VALIDACION", $"La fecha de la fila {row.RowNumber()} no es válida.");
            }

            var montoTexto = GetCellValue(row, headers, "Monto").Replace("S/", "", StringComparison.OrdinalIgnoreCase).Trim();
            decimal monto = 0;
            if (!string.IsNullOrWhiteSpace(montoTexto)
               && !decimal.TryParse(montoTexto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out monto))
            {
               await AuditarAsync(db, cargaArchivoId, periodo, nombreArchivo, "VALIDACION", $"El monto de la fila {row.RowNumber()} no es válido. Se usará 0.");
            }

            await db.ExecuteAsync(
               @"INSERT INTO DataProcesada (CargaArchivoId, CodigoProducto, FilaExcel, Descripcion, Monto, FechaTransaccion, CreatedAt)
                 VALUES (@CargaId, @Codigo, @Fila, @Descripcion, @Monto, GETDATE(),GETDATE())",
               new
               {
                  CargaId = cargaArchivoId,
                  Codigo = codigoProducto,
                  Fila = row.RowNumber(),
                  Descripcion = GetCellValue(row, headers, "Descripcion"),
                  Monto = monto

               });
            filasInsertadas++;
         }

         if (filasInsertadas == 0 && filasExistentes > 0)
         {
            await db.ExecuteAsync("UPDATE CargaArchivo SET Estado = 'Rechazado' WHERE Id = @Id", new { Id = cargaArchivoId });
            return new ProcesamientoResultado(Exito: false, filasInsertadas, filasExistentes);
         }

         await db.ExecuteAsync("UPDATE CargaArchivo SET Estado = 'Finalizado' WHERE Id = @Id", new { Id = cargaArchivoId });
         await _notificationPublisher.PublicarAsync(cargaArchivoId, periodo, await ObtenerUsuarioAsync(db, cargaArchivoId));
         return new ProcesamientoResultado(Exito: true, filasInsertadas, filasExistentes);
      }
      finally
      {
         if (!string.Equals(archivoParaProcesar, rutaArchivo, StringComparison.OrdinalIgnoreCase))
            File.Delete(archivoParaProcesar);
      }

   }

   private static async Task<string> ObtenerUsuarioAsync(SqlConnection db, int cargaArchivoId)
   {
      return await db.ExecuteScalarAsync<string>(
         "SELECT Usuario FROM CargaArchivo WHERE Id = @Id",
         new { Id = cargaArchivoId }) ?? "";
   }

   private static string GetCellValue(IXLRow row, IReadOnlyDictionary<string, int> headers, string name, string alternateName = "", string defaultValue = "")
   {
      var found = headers.TryGetValue(name, out var column)
         || (!string.IsNullOrWhiteSpace(alternateName) && headers.TryGetValue(alternateName, out column));
      return found
         && !string.IsNullOrWhiteSpace(row.Cell(column).GetString())
         ? row.Cell(column).GetString().Trim()
         : defaultValue;
   }

   private async Task AuditarAsync(SqlConnection db, int cargaArchivoId, string periodo, string nombreArchivo, string resultado, string motivo)
   {
      var usuario = await ObtenerUsuarioAsync(db, cargaArchivoId);
      await db.ExecuteAsync(
             "dbo.sp_InsertarCargaAuditoria",
             new { Periodo = periodo, NombreArchivo = nombreArchivo, Usuario = usuario, Resultado = resultado, Motivo = motivo, CargaArchivoId = cargaArchivoId },
             commandType: CommandType.StoredProcedure);
   }

   private async Task<string?> ResolverRutaArchivoAsync(string rutaArchivo)
   {
      if (string.IsNullOrWhiteSpace(rutaArchivo)) return null;
      if (!rutaArchivo.StartsWith("seaweed://", StringComparison.OrdinalIgnoreCase))
      {
         return File.Exists(rutaArchivo) ? rutaArchivo : null;
      }

      var recurso = rutaArchivo["seaweed://".Length..].TrimStart('/');
      var url = $"{_seaweedBaseUrl.TrimEnd('/')}/{recurso}";
      using var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return null;

      var extension = Path.GetExtension(recurso);
      var archivoTemporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
      await using var source = await response.Content.ReadAsStreamAsync();
      await using var target = File.Create(archivoTemporal);
      await source.CopyToAsync(target);
      return archivoTemporal;
   }
}

public record ProcesamientoResultado(bool Exito, int FilasInsertadas, int FilasExistentes)
{
   public bool TodosExistentes => FilasInsertadas == 0 && FilasExistentes > 0;

   public static ProcesamientoResultado Fallido() => new(false, 0, 0);
}