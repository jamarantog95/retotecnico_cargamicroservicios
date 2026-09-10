namespace ControlMicroservice.Services;

using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using ControlMicroservice.DTOs;

public class ControlService
{
   private readonly string _connectionString;
   private readonly IWebHostEnvironment _env;

   public ControlService(IConfiguration configuration, IWebHostEnvironment env)
   {
      _connectionString = configuration.GetConnectionString("DefaultConnection")!;
      _env = env;
   }

   public async Task<int> ProcessUploadAsync(UploadRequestDto request, string username)
   {
      if (request.File == null || request.File.Length == 0)
         throw new ArgumentException("El archivo Excel está vacío o no fue adjuntado.");

      var periodo = request.Periodo?.Trim();
      if (string.IsNullOrWhiteSpace(periodo) || string.Equals(periodo, "string", StringComparison.OrdinalIgnoreCase))
         throw new ArgumentException("Debe enviar un período válido en el campo 'Periodo' del formulario.");

      // 1. Validar extensión .xlsx o .xls
      var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
      if (extension != ".xlsx" && extension != ".xls")
         throw new InvalidOperationException("Solo se permiten archivos con formato Excel (.xlsx / .xls).");

      using IDbConnection db = new SqlConnection(_connectionString);

      // 2. Validar duplicidad de periodo usando el Stored Procedure
      var parameters = new DynamicParameters();
      parameters.Add("@Periodo", periodo);
      parameters.Add("@ResultadoAccion", dbType: DbType.String, size: 50, direction: ParameterDirection.Output);

      await db.ExecuteAsync("sp_ValidarDuplicidadPeriodo", parameters, commandType: CommandType.StoredProcedure);
      string resultadoAccion = parameters.Get<string>("@ResultadoAccion");

      if (resultadoAccion == "RECHAZADO")
         throw new InvalidOperationException($"Ya existe una carga exitosa o finalizada para el periodo {periodo}.");

      if (resultadoAccion == "BLOQUEADO")
         throw new InvalidOperationException($"Ya hay una carga en proceso para el periodo {periodo}. Intente más tarde.");

      // 3. Guardar el archivo físicamente en una carpeta temporal o de cargas
      var uploadsFolder = Path.Combine(_env.ContentRootPath, "Uploads");
      if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

      var uniqueFileName = $"{Guid.NewGuid()}_{request.File.FileName}";
      var filePath = Path.Combine(uploadsFolder, uniqueFileName);

      using (var stream = new FileStream(filePath, FileMode.Create))
      {
         await request.File.CopyToAsync(stream);
      }

      // 4. Registrar la cabecera en la tabla CargaArchivo mediante SP y obtener el ID
      var insertParams = new DynamicParameters();
      insertParams.Add("@NombreArchivo", request.File.FileName);
      insertParams.Add("@RutaArchivo", filePath);
      insertParams.Add("@Usuario", username);
      insertParams.Add("@Periodo", periodo);
      insertParams.Add("@NuevoId", dbType: DbType.Int32, direction: ParameterDirection.Output);

      await db.ExecuteAsync("sp_RegistrarCargaArchivo", insertParams, commandType: CommandType.StoredProcedure);
      int cargaId = insertParams.Get<int>("@NuevoId");

      return cargaId;
   }
}