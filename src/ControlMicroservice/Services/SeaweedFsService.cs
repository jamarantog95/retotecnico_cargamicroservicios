using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class SeaweedFsService
{
   private readonly HttpClient _httpClient;
   private readonly string _seaweedBaseUrl;

   public SeaweedFsService(HttpClient httpClient, IConfiguration configuration)
   {
      _httpClient = httpClient;
      _seaweedBaseUrl = configuration["SeaweedFS:BaseUrl"] ?? "http://localhost:8888";
   }

   public async Task<string> SubirArchivoAsync(IFormFile file)
   {
      if (file == null || file.Length == 0)
      {
         throw new ArgumentException("El archivo está vacío o es nulo.");
      }

      // Definimos la ruta o carpeta dentro de SeaweedFS donde se guardarán los uploads
      string uploadUrl = $"{_seaweedBaseUrl}/uploads/{file.FileName}";

      using var content = new MultipartFormDataContent();
      using var fileStream = file.OpenReadStream();
      using var streamContent = new StreamContent(fileStream);

      content.Add(streamContent, "file", file.FileName);

      // Envía la petición PUT o POST a SeaweedFS (el Filer soporta PUT/POST para almacenamiento de archivos)
      var response = await _httpClient.PostAsync(uploadUrl, content);

      if (!response.IsSuccessStatusCode)
      {
         var errorDetails = await response.Content.ReadAsStringAsync();
         throw new Exception($"Error al subir el archivo a SeaweedFS: {response.ReasonPhrase} - {errorDetails}");
      }

      var jsonResponse = await response.Content.ReadAsStringAsync();

      // Opcional: Puedes parsear la respuesta JSON que te devuelve SeaweedFS si trae la URL pública o path exacto.
      // Por ahora, retornamos la estructura de ruta requerida o la URL directa devuelta por el servidor.
      return $"seaweed://uploads/{file.FileName}";
   }
}