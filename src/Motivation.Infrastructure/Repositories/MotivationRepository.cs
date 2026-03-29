using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Motivation.Domain.Interfaces;
using Motivation.Infrastructure.Db;

namespace Motivation.Infrastructure.Repositories
{
    public class MotivationRepository : IMotivationRepository
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public MotivationRepository(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task AddAsync(Motivation.Domain.Entities.Motivation motivation)
        {
            await _db.Motivations.AddAsync(motivation);
            await _db.SaveChangesAsync();
            InvalidateGoalCache(motivation.GoalId);
        }

        public async Task<Motivation.Domain.Entities.Motivation?> GetByIdAsync(Guid motivationId)
        {
            return await _db.Motivations.FindAsync(motivationId);
        }

        public async Task<Motivation.Domain.Entities.Motivation[]> GetByGoalAsync(Guid goalId)
        {
            var key = GetCacheKey(goalId);
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _db.Motivations.Where(m => m.GoalId == goalId).ToArrayAsync();
            });
        }

        public async Task DeleteAsync(Guid motivationId)
        {
            var mot = await _db.Motivations.FindAsync(motivationId);
            if (mot != null)
            {
                _db.Motivations.Remove(mot);
                await _db.SaveChangesAsync();
                InvalidateGoalCache(mot.GoalId);
            }
        }

        private void InvalidateGoalCache(Guid goalId)
        {
            var key = GetCacheKey(goalId);
            _cache.Remove(key);
        }

        private static string GetCacheKey(Guid goalId) => $"motivations:goal:{goalId}";
    }
}
