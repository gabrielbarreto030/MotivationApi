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
    public class GoalRepository : IGoalRepository
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public GoalRepository(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task AddAsync(Goal goal)
        {
            await _db.Goals.AddAsync(goal);
            await _db.SaveChangesAsync();
            // invalidate cache for the user
            var key = GetCacheKey(goal.UserId);
            _cache.Remove(key);
        }

        public async Task<Goal[]> GetByUserAsync(Guid userId)
        {
            var key = GetCacheKey(userId);
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _db.Goals.Where(g => g.UserId == userId).ToArrayAsync();
            });
        }

        private static string GetCacheKey(Guid userId) => $"goals:{userId}";
    }
}
