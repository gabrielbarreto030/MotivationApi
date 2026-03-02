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
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            });
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            var key = GetEmailCacheKey(email);
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            });
        }

        private static string GetIdCacheKey(Guid userId) => $"user:id:{userId}";
        private static string GetEmailCacheKey(string email) => $"user:email:{email}";
    }
}
