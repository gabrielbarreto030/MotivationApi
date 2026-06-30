using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IGoalTimelineService
    {
        Task<GoalTimelineResponse?> GetTimelineAsync(Guid userId, Guid goalId);
    }
}
