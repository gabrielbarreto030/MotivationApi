using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IWeeklyReportService
    {
        Task<WeeklyReportResponse> GetWeeklyReportAsync(Guid userId);
    }
}
