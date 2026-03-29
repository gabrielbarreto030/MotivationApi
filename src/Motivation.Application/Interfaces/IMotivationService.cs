using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IMotivationService
    {
        Task<AddMotivationResponse> AddAsync(Guid goalId, AddMotivationRequest request, Guid userId);
        Task RemoveAsync(Guid goalId, Guid motivationId, Guid userId);
    }
}
