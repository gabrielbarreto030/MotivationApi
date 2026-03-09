using System;
using System.Threading.Tasks;
using Motivation.Domain.Entities;

namespace Motivation.Domain.Interfaces
{
    public interface IGoalRepository
    {
        Task AddAsync(Goal goal);
        Task<Goal[]> GetByUserAsync(Guid userId);
        Task<Goal?> GetByIdAsync(Guid id);
        Task UpdateAsync(Goal goal);
    }
}
