using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class GoalSummaryTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;

        public GoalSummaryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Summary_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _goalService = new GoalService(_goalRepository, _stepRepository, NullLogger<GoalService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> CreateGoalAsync(Guid userId, GoalStatus status = GoalStatus.Pending)
        {
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "Desc", status, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        private async Task<Step> CreateStepAsync(Guid goalId, bool completed = false)
        {
            var step = new Step(Guid.NewGuid(), goalId, "Step");
            await _stepRepository.AddAsync(step);
            if (completed)
            {
                step.MarkCompleted(DateTime.UtcNow);
                await _stepRepository.UpdateAsync(step);
            }
            return step;
        }

        [Fact]
        public async Task GetSummaryAsync_NoGoals_ReturnsZeroes()
        {
            var userId = Guid.NewGuid();

            var result = await _goalService.GetSummaryAsync(userId);

            result.TotalGoals.Should().Be(0);
            result.Pending.Should().Be(0);
            result.InProgress.Should().Be(0);
            result.Completed.Should().Be(0);
            result.Cancelled.Should().Be(0);
            result.TotalSteps.Should().Be(0);
            result.CompletedSteps.Should().Be(0);
            result.OverallCompletionRate.Should().Be(0);
        }

        [Fact]
        public async Task GetSummaryAsync_MultipleGoalsByStatus_CountsCorrectly()
        {
            var userId = Guid.NewGuid();
            await CreateGoalAsync(userId, GoalStatus.Pending);
            await CreateGoalAsync(userId, GoalStatus.Pending);
            await CreateGoalAsync(userId, GoalStatus.InProgress);
            await CreateGoalAsync(userId, GoalStatus.Completed);
            await CreateGoalAsync(userId, GoalStatus.Cancelled);

            var result = await _goalService.GetSummaryAsync(userId);

            result.TotalGoals.Should().Be(5);
            result.Pending.Should().Be(2);
            result.InProgress.Should().Be(1);
            result.Completed.Should().Be(1);
            result.Cancelled.Should().Be(1);
        }

        [Fact]
        public async Task GetSummaryAsync_WithSteps_CountsTotalAndCompleted()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            await CreateStepAsync(goal.Id, completed: true);
            await CreateStepAsync(goal.Id, completed: true);
            await CreateStepAsync(goal.Id, completed: false);

            var result = await _goalService.GetSummaryAsync(userId);

            result.TotalSteps.Should().Be(3);
            result.CompletedSteps.Should().Be(2);
        }

        [Fact]
        public async Task GetSummaryAsync_AllStepsCompleted_Returns100Percent()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            await CreateStepAsync(goal.Id, completed: true);
            await CreateStepAsync(goal.Id, completed: true);

            var result = await _goalService.GetSummaryAsync(userId);

            result.OverallCompletionRate.Should().Be(100);
        }

        [Fact]
        public async Task GetSummaryAsync_HalfStepsCompleted_Returns50Percent()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            await CreateStepAsync(goal.Id, completed: true);
            await CreateStepAsync(goal.Id, completed: false);

            var result = await _goalService.GetSummaryAsync(userId);

            result.OverallCompletionRate.Should().Be(50);
        }

        [Fact]
        public async Task GetSummaryAsync_StepsAcrossMultipleGoals_AggregatesCorrectly()
        {
            var userId = Guid.NewGuid();
            var goal1 = await CreateGoalAsync(userId);
            var goal2 = await CreateGoalAsync(userId);

            await CreateStepAsync(goal1.Id, completed: true);
            await CreateStepAsync(goal1.Id, completed: false);
            await CreateStepAsync(goal2.Id, completed: true);
            await CreateStepAsync(goal2.Id, completed: true);

            var result = await _goalService.GetSummaryAsync(userId);

            result.TotalSteps.Should().Be(4);
            result.CompletedSteps.Should().Be(3);
            result.OverallCompletionRate.Should().Be(75);
        }

        [Fact]
        public async Task GetSummaryAsync_IsolatedByUser_OtherUserGoalsIgnored()
        {
            var userId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            await CreateGoalAsync(userId, GoalStatus.InProgress);
            await CreateGoalAsync(otherId, GoalStatus.Pending);
            await CreateGoalAsync(otherId, GoalStatus.Completed);

            var result = await _goalService.GetSummaryAsync(userId);

            result.TotalGoals.Should().Be(1);
            result.InProgress.Should().Be(1);
            result.Pending.Should().Be(0);
            result.Completed.Should().Be(0);
        }

        [Fact]
        public async Task GetSummaryAsync_NoSteps_CompletionRateIsZero()
        {
            var userId = Guid.NewGuid();
            await CreateGoalAsync(userId);
            await CreateGoalAsync(userId);

            var result = await _goalService.GetSummaryAsync(userId);

            result.TotalGoals.Should().Be(2);
            result.TotalSteps.Should().Be(0);
            result.OverallCompletionRate.Should().Be(0);
        }
    }
}
