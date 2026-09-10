namespace AuthMicroservice.Infrastructure.Persistence;

using System.Data;
using Dapper;
using AuthMicroservice.Application.Interfaces;
using AuthMicroservice.Domain.Entities;

public class UserRepository : IUserRepository
{
   private readonly IDbConnection _dbConnection;

   public UserRepository(IDbConnection dbConnection)
   {
      _dbConnection = dbConnection;
   }

   public async Task<User?> GetByUsernameAsync(string username)
   {
      var query = "SELECT Id, Username, PasswordHash, Email, Roles, CreatedAt FROM Users WHERE Username = @Username";
      return await _dbConnection.QueryFirstOrDefaultAsync<User>(query, new { Username = username });
   }
}