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
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public UserRepository(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task AddAsync(User user)
        {
            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
            _cache.Remove(GetIdCacheKey(user.Id));
            _cache.Remove(GetEmailCacheKey(user.Email));
        }

        public async Task UpdateEmailAsync(User user, string oldEmail)
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
            _cache.Remove(GetIdCacheKey(user.Id));
            _cache.Remove(GetEmailCacheKey(oldEmail));
            _cache.Remove(GetEmailCacheKey(user.Email));
        }

        public async Task DeleteAsync(User user)
        {
            var goalIds = await _db.Goals
                .Where(g => g.UserId == user.Id)
                .Select(g => g.Id)
                .ToArrayAsync();

            if (goalIds.Length > 0)
            {
                var motivations = await _db.Motivations
                    .Where(m => goalIds.Contains(m.GoalId))
                    .ToListAsync();
                _db.Motivations.RemoveRange(motivations);

                var steps = await _db.Steps
                    .Where(s => goalIds.Contains(s.GoalId))
                    .ToListAsync();
                _db.Steps.RemoveRange(steps);

                var goals = await _db.Goals
                    .Where(g => g.UserId == user.Id)
                    .ToListAsync();
                _db.Goals.RemoveRange(goals);
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            _cache.Remove(GetIdCacheKey(user.Id));
            _cache.Remove(GetEmailCacheKey(user.Email));
            _cache.Remove($"goals:{user.Id}");
            foreach (var goalId in goalIds)
                _cache.Remove($"goal:{goalId}");
        }

        public async Task<User> GetByIdAsync(Guid userId)
        {
            var key = GetIdCacheKey(userId);
            if (_cache.TryGetValue<User>(key, out var cached) && cached != null)
                return cached;

            var found = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (found != null)
                _cache.Set(key, found, TimeSpan.FromMinutes(10));

            return found;
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            var key = GetEmailCacheKey(email);
            if (_cache.TryGetValue<User>(key, out var cached) && cached != null)
                return cached;

            var found = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (found != null)
                _cache.Set(key, found, TimeSpan.FromMinutes(10));

            return found;
        }

        private static string GetIdCacheKey(Guid userId) => $"user:id:{userId}";
        private static string GetEmailCacheKey(string email) => $"user:email:{email}";
    }
}
