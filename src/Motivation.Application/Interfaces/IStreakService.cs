using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IStreakService
    {
        Task<UserStreakResponse> GetStreakAsync(Guid userId);
    }
}
