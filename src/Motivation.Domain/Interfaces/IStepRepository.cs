using System;
using System.Threading.Tasks;
using Motivation.Domain.Entities;

namespace Motivation.Domain.Interfaces
{
    public interface IStepRepository
    {
        Task AddAsync(Step step);
        Task<Step[]> GetByGoalAsync(Guid goalId);
        Task UpdateAsync(Step step);
    }
}
