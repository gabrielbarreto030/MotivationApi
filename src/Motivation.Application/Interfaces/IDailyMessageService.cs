using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IDailyMessageService
    {
        Task<DailyMessageResponse> GetDailyMessageAsync(Guid userId, DateOnly? date = null);
    }
}
