using System;
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
