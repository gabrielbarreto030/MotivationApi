using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IGoalService
    {
        Task<CreateGoalResponse> CreateAsync(CreateGoalRequest request, Guid userId);
        Task<CreateGoalResponse[]> ListByUserAsync(Guid userId);
        Task<UpdateGoalResponse> UpdateAsync(Guid id, UpdateGoalRequest request, Guid userId);
        Task DeleteAsync(Guid id, Guid userId);
    }
}