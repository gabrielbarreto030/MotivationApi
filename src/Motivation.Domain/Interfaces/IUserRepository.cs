using System;
using System.Threading.Tasks;
using Motivation.Domain.Entities;

namespace Motivation.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task AddAsync(User user);
        Task<User> GetByIdAsync(Guid userId);
        Task<User> GetByEmailAsync(string email);
    }
}
