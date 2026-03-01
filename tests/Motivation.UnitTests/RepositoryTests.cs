using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class RepositoryTests
    {
        [Fact]
        public async Task GoalRepository_AddAndGetByUser_WorksWithCache()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
                .Options;

            using var context = new AppDbContext(options);
            var mem = new MemoryCache(new MemoryCacheOptions());
            var repo = new GoalRepository(context, mem);

            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Title1", "Desc1", GoalStatus.Pending, DateTime.UtcNow);

            await repo.AddAsync(goal);

            var fetched = await repo.GetByUserAsync(userId);
            fetched.Should().ContainSingle()
                .Which.Title.Should().Be("Title1");

            // second call should hit cache (no exceptions, same result)
            var fetched2 = await repo.GetByUserAsync(userId);
            fetched2.Should().HaveCount(1);
        }
    }
}
