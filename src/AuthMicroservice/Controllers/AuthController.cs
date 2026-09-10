namespace AuthMicroservice.Controllers;

using Microsoft.AspNetCore.Mvc;
using AuthMicroservice.Application.DTOs;
using AuthMicroservice.Application.Interfaces;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
   private readonly IAuthService _authService;

   public AuthController(IAuthService authService)
   {
      _authService = authService;
   }

   [HttpPost("login")]
   public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
   {
      try
      {
         var response = await _authService.LoginAsync(request);
         return Ok(response);
      }
      catch (UnauthorizedAccessException ex)
      {
         return Unauthorized(new { message = ex.Message });
      }
   }
}