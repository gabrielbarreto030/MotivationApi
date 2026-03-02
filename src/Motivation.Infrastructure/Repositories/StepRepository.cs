using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Motivation.Domain.Entities;
using Motivation.Domain.Interfaces;
using Motivation.Infrastructure.Db;

namespace Motivation.Infrastructure.Repositories
{
    public class StepRepository : IStepRepository
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public StepRepository(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task AddAsync(Step step)
        {
            await _db.Steps.AddAsync(step);
            await _db.SaveChangesAsync();
            InValidateGoalCache(step.GoalId);
        }

        public async Task<Step[]> GetByGoalAsync(Guid goalId)
        {
            var key = GetCacheKey(goalId);
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _db.Steps.Where(s => s.GoalId == goalId).ToArrayAsync();
            });
        }

        public async Task UpdateAsync(Step step)
        {
            _db.Steps.Update(step);
            await _db.SaveChangesAsync();
            InValidateGoalCache(step.GoalId);
        }

        private void InValidateGoalCache(Guid goalId)
        {
            var key = GetCacheKey(goalId);
            _cache.Remove(key);
        }

        private static string GetCacheKey(Guid goalId) => $"steps:goal:{goalId}";
    }
}
