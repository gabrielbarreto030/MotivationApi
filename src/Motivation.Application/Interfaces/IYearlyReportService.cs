using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IYearlyReportService
    {
        Task<YearlyReportResponse> GetYearlyReportAsync(Guid userId);
    }
}
