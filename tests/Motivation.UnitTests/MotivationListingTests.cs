using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Motivation.Application.DTOs;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class MotivationListingTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly MotivationService _motivationService;

        public MotivationListingTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_MotivationListing_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _motivationRepository = new MotivationRepository(_context, _cache);
            _motivationService = new MotivationService(_motivationRepository, _goalRepository, NullLogger<MotivationService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> CreateGoalAsync(Guid userId, string title = "Goal")
        {
            var goal = new Goal(Guid.NewGuid(), userId, title, "Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        [Fact]
        public async Task ListByGoalAsync_NoMotivations_ReturnsEmptyArray()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var result = await _motivationService.ListByGoalAsync(goal.Id, userId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ListByGoalAsync_WithMotivations_ReturnsAll()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("First"), userId);
            await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Second"), userId);
            await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Third"), userId);

            var result = await _motivationService.ListByGoalAsync(goal.Id, userId);

            result.Should().HaveCount(3);
            result.Should().Contain(m => m.Text == "First");
            result.Should().Contain(m => m.Text == "Second");
            result.Should().Contain(m => m.Text == "Third");
        }

        [Fact]
        public async Task ListByGoalAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();

            Func<Task> act = async () => await _motivationService.ListByGoalAsync(Guid.NewGuid(), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task ListByGoalAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var goal = await CreateGoalAsync(ownerId);

            Func<Task> act = async () => await _motivationService.ListByGoalAsync(goal.Id, otherId);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task ListByGoalAsync_ResponseContainsCorrectFields()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Keep going!"), userId);

            var result = await _motivationService.ListByGoalAsync(goal.Id, userId);

            result.Should().HaveCount(1);
            result[0].Id.Should().NotBeEmpty();
            result[0].GoalId.Should().Be(goal.Id);
            result[0].Text.Should().Be("Keep going!");
        }

        [Fact]
        public async Task ListByGoalAsync_IsolatedPerGoal_ReturnsOnlyOwnMotivations()
        {
            var userId = Guid.NewGuid();
            var goal1 = await CreateGoalAsync(userId, "Goal 1");
            var goal2 = await CreateGoalAsync(userId, "Goal 2");

            await _motivationService.AddAsync(goal1.Id, new AddMotivationRequest("For goal 1"), userId);
            await _motivationService.AddAsync(goal2.Id, new AddMotivationRequest("For goal 2a"), userId);
            await _motivationService.AddAsync(goal2.Id, new AddMotivationRequest("For goal 2b"), userId);

            var result1 = await _motivationService.ListByGoalAsync(goal1.Id, userId);
            var result2 = await _motivationService.ListByGoalAsync(goal2.Id, userId);

            result1.Should().HaveCount(1);
            result2.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByGoalAsync_AfterDelete_ReflectsRemoval()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var added = await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("To be removed"), userId);
            await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("To keep"), userId);

            await _motivationService.RemoveAsync(goal.Id, added.Id, userId);

            var result = await _motivationService.ListByGoalAsync(goal.Id, userId);

            result.Should().HaveCount(1);
            result.Should().NotContain(m => m.Id == added.Id);
            result.Should().Contain(m => m.Text == "To keep");
        }
    }
}
