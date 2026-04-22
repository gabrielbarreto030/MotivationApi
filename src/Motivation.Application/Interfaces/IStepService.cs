using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IStepService
    {
        Task<CreateStepResponse> CreateAsync(Guid goalId, CreateStepRequest request, Guid userId);
        Task<CreateStepResponse[]> ListByGoalAsync(Guid goalId, Guid userId);
        Task<PagedResponse<CreateStepResponse>> ListByGoalPagedAsync(Guid goalId, Guid userId, PagedRequest request);
        Task<PagedResponse<CreateStepResponse>> ListByGoalFilteredAsync(Guid goalId, Guid userId, StepFilterRequest request);
        Task<CreateStepResponse> MarkCompletedAsync(Guid goalId, Guid stepId, Guid userId);
        Task<CreateStepResponse> UpdateAsync(Guid goalId, Guid stepId, UpdateStepRequest request, Guid userId);
    }
}
