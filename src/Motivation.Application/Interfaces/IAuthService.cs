using System.Threading.Tasks;
using Motivation.Application.DTOs;
using Motivation.Domain.Entities;

namespace Motivation.Application.Interfaces
{
    public interface IAuthService
    {
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<User> ValidateCredentialsAsync(LoginRequest request);
    }
}