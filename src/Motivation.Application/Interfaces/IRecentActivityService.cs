using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IRecentActivityService
    {
        Task<RecentActivityResponse> GetRecentActivityAsync(Guid userId, int page, int pageSize);
    }
}
