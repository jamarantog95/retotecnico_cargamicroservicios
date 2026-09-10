namespace AuthMicroservice.Infrastructure.Security;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using AuthMicroservice.Domain.Entities;

public class TokenService
{
   private readonly IConfiguration _configuration;

   public TokenService(IConfiguration configuration)
   {
      _configuration = configuration;
   }

   public string GenerateToken(User user, out DateTime expiration)
   {
      var jwtSettings = _configuration.GetSection("JwtSettings");
      var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
      expiration = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["DurationInMinutes"]!));

      var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Roles)
        };

      var key = new SymmetricSecurityKey(secretKey);
      var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

      var tokenDescriptor = new SecurityTokenDescriptor
      {
         Subject = new ClaimsIdentity(claims),
         Expires = expiration,
         Issuer = jwtSettings["Issuer"],
         Audience = jwtSettings["Audience"],
         SigningCredentials = creds
      };

      var tokenHandler = new JwtSecurityTokenHandler();
      var token = tokenHandler.CreateToken(tokenDescriptor);

      return tokenHandler.WriteToken(token);
   }
}