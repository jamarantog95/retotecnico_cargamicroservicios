using Microsoft.AspNetCore.Mvc;
using BulkProcessMicroservice.Services;

[Route("api/process")]
[ApiController]
public class ProcessController : ControllerBase
{
   private readonly ExcelProcessService _processService;

   public ProcessController(ExcelProcessService processService)
   {
      _processService = processService;
   }

   [HttpPost("ejecutar")]
   public async Task<IActionResult> EjecutarProceso([FromBody] ProcessRequest request)
   {
      var resultado = await _processService.ProcesarArchivoExcelAsync(request.CargaArchivoId, request.RutaArchivo, request.Periodo);

      if (resultado.TodosExistentes)
         return BadRequest(new { success = false, message = "Los registros del archivo ya existen. No se cargó ningún dato nuevo." });

      if (!resultado.Exito)
         return BadRequest(new { success = false, message = "El proceso falló por validaciones de negocio (duplicidad de periodo o archivo no encontrado)." });

      return Ok(new { success = true, message = "Archivo procesado y cargado exitosamente." });
   }
}

public class ProcessRequest
{
   public int CargaArchivoId { get; set; }
   public string RutaArchivo { get; set; } = string.Empty;
   public string Periodo { get; set; } = string.Empty;
}