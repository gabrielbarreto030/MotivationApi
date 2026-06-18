using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IDailySummaryService
    {
        Task<DailySummaryResponse> GetDailySummaryAsync(Guid userId, DateOnly date);
    }
}
