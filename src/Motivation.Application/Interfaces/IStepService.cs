using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IStepService
    {
        Task<CreateStepResponse> CreateAsync(Guid goalId, CreateStepRequest request, Guid userId);
        Task<CreateStepResponse[]> ListByGoalAsync(Guid goalId, Guid userId);
        Task<CreateStepResponse> MarkCompletedAsync(Guid goalId, Guid stepId, Guid userId);
    }
}
