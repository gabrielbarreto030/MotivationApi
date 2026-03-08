using System.Threading.Tasks;
using Motivation.Application.DTOs;

namespace Motivation.Application.Interfaces
{
    public interface IGoalService
    {
        Task<CreateGoalResponse> CreateAsync(CreateGoalRequest request, Guid userId);
    }
}