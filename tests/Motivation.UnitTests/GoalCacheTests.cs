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
    public class GoalCacheTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _repository;

        public GoalCacheTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_GoalCache_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _repository = new GoalRepository(_context, _cache);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        [Fact]
        public async Task GetByUserAsync_SecondCall_ReturnsCachedResults()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Cache Test", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _repository.AddAsync(goal);

            // First call populates cache
            var firstCall = await _repository.GetByUserAsync(userId);
            firstCall.Should().HaveCount(1);

            // Add directly to DB bypassing repository (cache should still return old data)
            var bypass = new Goal(Guid.NewGuid(), userId, "Bypass", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _context.Goals.AddAsync(bypass);
            await _context.SaveChangesAsync();

            // Second call should return cached (1 item, not 2)
            var secondCall = await _repository.GetByUserAsync(userId);
            secondCall.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetByUserAsync_AfterCreate_CacheIsInvalidated()
        {
            var userId = Guid.NewGuid();
            var g1 = new Goal(Guid.NewGuid(), userId, "First", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _repository.AddAsync(g1);

            var firstCall = await _repository.GetByUserAsync(userId);
            firstCall.Should().HaveCount(1);

            // AddAsync invalidates cache
            var g2 = new Goal(Guid.NewGuid(), userId, "Second", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _repository.AddAsync(g2);

            var afterAdd = await _repository.GetByUserAsync(userId);
            afterAdd.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetByUserAsync_AfterUpdate_CacheIsInvalidated()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Original", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _repository.AddAsync(goal);

            var before = await _repository.GetByUserAsync(userId);
            before[0].Title.Should().Be("Original");

            goal.Update("Updated", null, null);
            await _repository.UpdateAsync(goal);

            var after = await _repository.GetByUserAsync(userId);
            after[0].Title.Should().Be("Updated");
        }

        [Fact]
        public async Task GetByUserAsync_AfterDelete_CacheIsInvalidated()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "ToDelete", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _repository.AddAsync(goal);

            var before = await _repository.GetByUserAsync(userId);
            before.Should().HaveCount(1);

            await _repository.DeleteAsync(goal);

            var after = await _repository.GetByUserAsync(userId);
            after.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByIdAsync_SecondCall_ReturnsCachedResult()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "ById Test", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _repository.AddAsync(goal);

            // First call populates cache
            var firstCall = await _repository.GetByIdAsync(goal.Id);
            firstCall.Should().NotBeNull();
            firstCall!.Title.Should().Be("ById Test");

            // Modify directly in DB bypassing repository
            firstCall.Update("Changed", null, null);
            _context.Goals.Update(firstCall);
            await _context.SaveChangesAsync();

            // Should still return cached version
            var secondCall = await _repository.GetByIdAsync(goal.Id);
            secondCall!.Title.Should().Be("ById Test");
        }

        [Fact]
        public async Task GetByIdAsync_AfterUpdate_CacheIsInvalidated()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Before Update", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _repository.AddAsync(goal);

            var before = await _repository.GetByIdAsync(goal.Id);
            before!.Title.Should().Be("Before Update");

            goal.Update("After Update", null, null);
            await _repository.UpdateAsync(goal);

            var after = await _repository.GetByIdAsync(goal.Id);
            after!.Title.Should().Be("After Update");
        }

        [Fact]
        public async Task GetByIdAsync_AfterDelete_CacheIsInvalidated()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "ToDelete", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _repository.AddAsync(goal);

            var before = await _repository.GetByIdAsync(goal.Id);
            before.Should().NotBeNull();

            await _repository.DeleteAsync(goal);

            var after = await _repository.GetByIdAsync(goal.Id);
            after.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_NonExistentId_ReturnsNull()
        {
            var result = await _repository.GetByIdAsync(Guid.NewGuid());
            result.Should().BeNull();
        }
    }
}
