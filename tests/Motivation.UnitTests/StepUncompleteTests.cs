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
    public class StepUncompleteTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;

        public StepUncompleteTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_StepUncomplete_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context);
            _stepService = new StepService(_stepRepository, _goalRepository, NullLogger<StepService>.Instance);
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

        private async Task<CreateStepResponse> CreateAndCompleteStepAsync(Guid goalId, Guid userId, string title = "Step")
        {
            var step = await _stepService.CreateAsync(goalId, new CreateStepRequest(title), userId);
            return await _stepService.MarkCompletedAsync(goalId, step.Id, userId);
        }

        // Domain entity tests

        [Fact]
        public void Uncomplete_SetsIsCompletedFalse_AndClearsCompletedAt()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Step");
            step.MarkCompleted(DateTime.UtcNow);

            step.Uncomplete();

            step.IsCompleted.Should().BeFalse();
            step.CompletedAt.Should().BeNull();
        }

        [Fact]
        public void Uncomplete_OnIncompleteStep_SetsIsCompletedFalse()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Step");

            step.Uncomplete();

            step.IsCompleted.Should().BeFalse();
            step.CompletedAt.Should().BeNull();
        }

        // Service tests

        [Fact]
        public async Task UncompleteAsync_CompletedStep_ReturnsIncompleteStep()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var completed = await CreateAndCompleteStepAsync(goal.Id, userId);

            var result = await _stepService.UncompleteAsync(goal.Id, completed.Id, userId);

            result.IsCompleted.Should().BeFalse();
            result.CompletedAt.Should().BeNull();
            result.Id.Should().Be(completed.Id);
        }

        [Fact]
        public async Task UncompleteAsync_PersistsChange_VisibleInSubsequentList()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var completed = await CreateAndCompleteStepAsync(goal.Id, userId);

            await _stepService.UncompleteAsync(goal.Id, completed.Id, userId);

            var list = await _stepService.ListByGoalAsync(goal.Id, userId);
            list.Should().Contain(s => s.Id == completed.Id && !s.IsCompleted && s.CompletedAt == null);
        }

        [Fact]
        public async Task UncompleteAsync_NotCompleted_ThrowsInvalidOperationException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var step = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), userId);

            Func<Task> act = async () =>
                await _stepService.UncompleteAsync(goal.Id, step.Id, userId);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not completed*");
        }

        [Fact]
        public async Task UncompleteAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();

            Func<Task> act = async () =>
                await _stepService.UncompleteAsync(Guid.NewGuid(), Guid.NewGuid(), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task UncompleteAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var goal = await CreateGoalAsync(ownerId);
            var completed = await CreateAndCompleteStepAsync(goal.Id, ownerId);

            Func<Task> act = async () =>
                await _stepService.UncompleteAsync(goal.Id, completed.Id, otherId);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task UncompleteAsync_StepNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            Func<Task> act = async () =>
                await _stepService.UncompleteAsync(goal.Id, Guid.NewGuid(), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Step not found*");
        }

        [Fact]
        public async Task UncompleteAsync_StepBelongsToDifferentGoal_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal1 = await CreateGoalAsync(userId, "Goal 1");
            var goal2 = await CreateGoalAsync(userId, "Goal 2");
            var completed = await CreateAndCompleteStepAsync(goal1.Id, userId);

            Func<Task> act = async () =>
                await _stepService.UncompleteAsync(goal2.Id, completed.Id, userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not belong to the specified goal*");
        }

        [Fact]
        public async Task UncompleteAsync_AfterUncompletion_CanBeCompletedAgain()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var completed = await CreateAndCompleteStepAsync(goal.Id, userId);

            await _stepService.UncompleteAsync(goal.Id, completed.Id, userId);
            var reCompleted = await _stepService.MarkCompletedAsync(goal.Id, completed.Id, userId);

            reCompleted.IsCompleted.Should().BeTrue();
            reCompleted.CompletedAt.Should().NotBeNull();
        }
    }
}
