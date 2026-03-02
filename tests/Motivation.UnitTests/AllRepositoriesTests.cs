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
    public class AllRepositoriesTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public AllRepositoriesTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        [Fact]
        public async Task UserRepository_AddAndGet_Works()
        {
            var repo = new UserRepository(_context, _cache);
            var user = new User(Guid.NewGuid(), "test@example.com", "hashed_pw", DateTime.UtcNow);

            await repo.AddAsync(user);
            var retrieved = await repo.GetByIdAsync(user.Id);

            retrieved.Should().NotBeNull();
            retrieved.Email.Should().Be("test@example.com");
        }

        [Fact]
        public async Task UserRepository_GetByEmail_Works()
        {
            var repo = new UserRepository(_context, _cache);
            var user = new User(Guid.NewGuid(), "unique@test.com", "hashed_pw", DateTime.UtcNow);

            await repo.AddAsync(user);
            var retrieved = await repo.GetByEmailAsync("unique@test.com");

            retrieved.Should().NotBeNull();
            retrieved.Id.Should().Be(user.Id);
        }

        [Fact]
        public async Task StepRepository_AddAndGetByGoal_Works()
        {
            var repo = new StepRepository(_context, _cache);
            var goalId = Guid.NewGuid();
            var step = new Step(Guid.NewGuid(), goalId, "Step 1");

            await repo.AddAsync(step);
            var steps = await repo.GetByGoalAsync(goalId);

            steps.Should().ContainSingle().Which.Title.Should().Be("Step 1");
        }

        [Fact]
        public async Task StepRepository_UpdateInvalidatesCache()
        {
            var repo = new StepRepository(_context, _cache);
            var goalId = Guid.NewGuid();
            var step = new Step(Guid.NewGuid(), goalId, "Step 1");

            await repo.AddAsync(step);
            var firstFetch = await repo.GetByGoalAsync(goalId);
            firstFetch.Should().HaveCount(1);

            step.MarkCompleted(DateTime.UtcNow);
            await repo.UpdateAsync(step);

            // Cache should be invalidated, fetching fresh data
            var secondFetch = await repo.GetByGoalAsync(goalId);
            secondFetch.Should().HaveCount(1);
            secondFetch[0].IsCompleted.Should().BeTrue();
        }

        [Fact]
        public async Task MotivationRepository_AddAndGetByGoal_Works()
        {
            var repo = new MotivationRepository(_context, _cache);
            var goalId = Guid.NewGuid();
            var motivation = new Domain.Entities.Motivation(Guid.NewGuid(), goalId, "You can do it!");

            await repo.AddAsync(motivation);
            var motivations = await repo.GetByGoalAsync(goalId);

            motivations.Should().ContainSingle().Which.Text.Should().Be("You can do it!");
        }

        [Fact]
        public async Task MotivationRepository_Delete_Works()
        {
            var repo = new MotivationRepository(_context, _cache);
            var goalId = Guid.NewGuid();
            var motId = Guid.NewGuid();
            var motivation = new Domain.Entities.Motivation(motId, goalId, "Motivation text");

            await repo.AddAsync(motivation);
            await repo.DeleteAsync(motId);

            var remaining = await repo.GetByGoalAsync(goalId);
            remaining.Should().BeEmpty();
        }

        [Fact]
        public async Task GoalRepository_AddAndGetByUser_Works()
        {
            var repo = new GoalRepository(_context, _cache);
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            await repo.AddAsync(goal);
            var goals = await repo.GetByUserAsync(userId);

            goals.Should().ContainSingle().Which.Title.Should().Be("Title");
        }
    }
}
