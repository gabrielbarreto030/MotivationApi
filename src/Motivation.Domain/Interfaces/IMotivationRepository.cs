using System;
using System.Threading.Tasks;
using Motivation.Domain.Entities;

namespace Motivation.Domain.Interfaces
{
    public interface IMotivationRepository
    {
        Task AddAsync(Motivation.Domain.Entities.Motivation motivation);
        Task<Motivation.Domain.Entities.Motivation?> GetByIdAsync(Guid motivationId);
        Task<Motivation.Domain.Entities.Motivation[]> GetByGoalAsync(Guid goalId);
        Task DeleteAsync(Guid motivationId);
    }
}
