namespace AuthMicroservice.Application.Services;

using AuthMicroservice.Application.DTOs;
using AuthMicroservice.Application.Interfaces;
using AuthMicroservice.Infrastructure.Security;

public class AuthService : IAuthService
{
   private readonly IUserRepository _userRepository;
   private readonly TokenService _tokenService;

   public AuthService(IUserRepository userRepository, TokenService tokenService)
   {
      _userRepository = userRepository;
      _tokenService = tokenService;
   }

   public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
   {
      var user = await _userRepository.GetByUsernameAsync(request.Username);

      if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
      {
         throw new UnauthorizedAccessException("Credenciales inválidas.");
      }

      var token = _tokenService.GenerateToken(user, out var expiration);

      return new AuthResponseDto(token, expiration);
   }
}