using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IMotivationService
    {
        Task<AddMotivationResponse> AddAsync(Guid goalId, AddMotivationRequest request, Guid userId);
        Task RemoveAsync(Guid goalId, Guid motivationId, Guid userId);
        Task<AddMotivationResponse[]> ListByGoalAsync(Guid goalId, Guid userId);
        Task<PagedResponse<AddMotivationResponse>> ListByGoalFilteredAsync(Guid goalId, Guid userId, MotivationFilterRequest filter);
        Task<AddMotivationResponse> UpdateAsync(Guid goalId, Guid motivationId, UpdateMotivationRequest request, Guid userId);
        Task<AddMotivationResponse> FavoriteAsync(Guid goalId, Guid motivationId, Guid userId);
        Task<AddMotivationResponse> UnfavoriteAsync(Guid goalId, Guid motivationId, Guid userId);
    }
}
