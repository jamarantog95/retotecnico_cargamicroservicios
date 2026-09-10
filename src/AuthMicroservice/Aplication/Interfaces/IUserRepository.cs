namespace AuthMicroservice.Application.Interfaces;

using AuthMicroservice.Domain.Entities;

public interface IUserRepository
{
   Task<User?> GetByUsernameAsync(string username);
}