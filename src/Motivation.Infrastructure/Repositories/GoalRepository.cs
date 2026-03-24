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

        public async Task<Goal?> GetByIdAsync(Guid id)
        {
            var key = GetIdCacheKey(id);
            var cached = await _cache.GetOrCreateAsync<Goal?>(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                DetachIfTracked(id);
                return await _db.Goals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
            });

            if (cached == null) return null;

            // Detach any tracked entity so caller can safely call Update/Delete with the returned copy
            DetachIfTracked(id);

            // Return a defensive copy to prevent external mutations from corrupting the cache
            return new Goal(cached.Id, cached.UserId, cached.Title, cached.Description, cached.Status, cached.CreatedAt);
        }

        public async Task UpdateAsync(Goal goal)
        {
            DetachIfTracked(goal.Id);
            _db.Goals.Update(goal);
            await _db.SaveChangesAsync();
            _cache.Remove(GetCacheKey(goal.UserId));
            _cache.Remove(GetIdCacheKey(goal.Id));
        }

        public async Task DeleteAsync(Goal goal)
        {
            DetachIfTracked(goal.Id);
            _db.Goals.Remove(goal);
            await _db.SaveChangesAsync();
            _cache.Remove(GetCacheKey(goal.UserId));
            _cache.Remove(GetIdCacheKey(goal.Id));
        }

        private void DetachIfTracked(Guid id)
        {
            var entry = _db.ChangeTracker.Entries<Goal>()
                .FirstOrDefault(e => e.Entity.Id == id);
            if (entry != null)
                entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }

        private static string GetCacheKey(Guid userId) => $"goals:{userId}";
        private static string GetIdCacheKey(Guid id) => $"goal:{id}";
    }
}
