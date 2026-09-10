namespace AuthMicroservice.Application.Interfaces;

using AuthMicroservice.Application.DTOs;

public interface IAuthService
{
   Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
}