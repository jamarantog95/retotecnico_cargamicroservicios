namespace ControlMicroservice.DTOs;

public class UploadRequestDto
{
   public IFormFile File { get; set; } = null!;
   public string Periodo { get; set; } = string.Empty;
}