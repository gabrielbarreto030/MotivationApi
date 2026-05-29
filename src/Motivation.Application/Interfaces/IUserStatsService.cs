using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IUserStatsService
    {
        Task<UserStatsResponse> GetStatsAsync(Guid userId);
    }
}
