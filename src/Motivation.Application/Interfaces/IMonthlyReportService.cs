using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IMonthlyReportService
    {
        Task<MonthlyReportResponse> GetMonthlyReportAsync(Guid userId);
    }
}
