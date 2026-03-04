using Motivation.Domain.Entities;

namespace Motivation.Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
