using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IActivityHeatmapService
    {
        Task<ActivityHeatmapResponse> GetHeatmapAsync(Guid userId);
    }
}
