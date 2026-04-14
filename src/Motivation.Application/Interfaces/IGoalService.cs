using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IGoalService
    {
        Task<CreateGoalResponse> CreateAsync(CreateGoalRequest request, Guid userId);
        Task<CreateGoalResponse[]> ListByUserAsync(Guid userId);
        Task<PagedResponse<CreateGoalResponse>> ListByUserPagedAsync(Guid userId, PagedRequest request);
        Task<PagedResponse<CreateGoalResponse>> ListByUserFilteredAsync(Guid userId, GoalFilterRequest request);
        Task<UpdateGoalResponse> UpdateAsync(Guid id, UpdateGoalRequest request, Guid userId);
        Task DeleteAsync(Guid id, Guid userId);
        Task<GoalProgressResponse> GetProgressAsync(Guid goalId, Guid userId);
    }
}