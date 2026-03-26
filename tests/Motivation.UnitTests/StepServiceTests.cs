using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Motivation.Application.DTOs;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class StepServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;

        public StepServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Step_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _stepService = new StepService(_stepRepository, _goalRepository);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> CreateGoalAsync(Guid userId, string title = "Goal", string description = "Desc")
        {
            var goal = new Goal(Guid.NewGuid(), userId, title, description, GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_CreatesStep()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var request = new CreateStepRequest("First Step");
            var result = await _stepService.CreateAsync(goal.Id, request, userId);

            result.Id.Should().NotBeEmpty();
            result.GoalId.Should().Be(goal.Id);
            result.Title.Should().Be("First Step");
            result.IsCompleted.Should().BeFalse();
            result.CompletedAt.Should().BeNull();
        }

        [Fact]
        public async Task CreateAsync_EmptyTitle_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var request = new CreateStepRequest("");

            Func<Task> act = async () => await _stepService.CreateAsync(goal.Id, request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Title*");
        }

        [Fact]
        public async Task CreateAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var request = new CreateStepRequest("Step");

            Func<Task> act = async () => await _stepService.CreateAsync(Guid.NewGuid(), request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task CreateAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var goal = await CreateGoalAsync(ownerId);

            var request = new CreateStepRequest("Step");

            Func<Task> act = async () => await _stepService.CreateAsync(goal.Id, request, otherId);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task CreateAsync_PersistsStep_InRepository()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var request = new CreateStepRequest("My Step");
            var result = await _stepService.CreateAsync(goal.Id, request, userId);

            var steps = await _stepRepository.GetByGoalAsync(goal.Id);
            steps.Should().HaveCount(1);
            steps[0].Id.Should().Be(result.Id);
            steps[0].Title.Should().Be("My Step");
        }

        [Fact]
        public async Task CreateAsync_MultipleSteps_AllPersisted()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step 1"), userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step 2"), userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step 3"), userId);

            var steps = await _stepRepository.GetByGoalAsync(goal.Id);
            steps.Should().HaveCount(3);
        }

        [Fact]
        public async Task ListByGoalAsync_ValidGoal_ReturnsSteps()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step A"), userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step B"), userId);

            var result = await _stepService.ListByGoalAsync(goal.Id, userId);

            result.Should().HaveCount(2);
            result.Should().Contain(s => s.Title == "Step A");
            result.Should().Contain(s => s.Title == "Step B");
        }

        [Fact]
        public async Task ListByGoalAsync_EmptyGoal_ReturnsEmptyArray()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var result = await _stepService.ListByGoalAsync(goal.Id, userId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ListByGoalAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();

            Func<Task> act = async () => await _stepService.ListByGoalAsync(Guid.NewGuid(), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task ListByGoalAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var goal = await CreateGoalAsync(ownerId);

            Func<Task> act = async () => await _stepService.ListByGoalAsync(goal.Id, otherId);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task ListByGoalAsync_ReturnsCorrectFields()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("My Step"), userId);

            var result = await _stepService.ListByGoalAsync(goal.Id, userId);

            result.Should().HaveCount(1);
            result[0].GoalId.Should().Be(goal.Id);
            result[0].Title.Should().Be("My Step");
            result[0].IsCompleted.Should().BeFalse();
            result[0].CompletedAt.Should().BeNull();
        }

        // MarkCompletedAsync tests

        [Fact]
        public async Task MarkCompletedAsync_ValidStep_MarksAsCompleted()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step to complete"), userId);

            var result = await _stepService.MarkCompletedAsync(goal.Id, created.Id, userId);

            result.IsCompleted.Should().BeTrue();
            result.CompletedAt.Should().NotBeNull();
            result.Id.Should().Be(created.Id);
        }

        [Fact]
        public async Task MarkCompletedAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();

            Func<Task> act = async () => await _stepService.MarkCompletedAsync(Guid.NewGuid(), Guid.NewGuid(), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task MarkCompletedAsync_StepNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            Func<Task> act = async () => await _stepService.MarkCompletedAsync(goal.Id, Guid.NewGuid(), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Step not found*");
        }

        [Fact]
        public async Task MarkCompletedAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var goal = await CreateGoalAsync(ownerId);
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), ownerId);

            Func<Task> act = async () => await _stepService.MarkCompletedAsync(goal.Id, created.Id, otherId);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task MarkCompletedAsync_AlreadyCompleted_ThrowsInvalidOperationException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), userId);

            await _stepService.MarkCompletedAsync(goal.Id, created.Id, userId);

            Func<Task> act = async () => await _stepService.MarkCompletedAsync(goal.Id, created.Id, userId);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already completed*");
        }

        [Fact]
        public async Task MarkCompletedAsync_StepBelongsToDifferentGoal_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal1 = await CreateGoalAsync(userId, "Goal 1");
            var goal2 = await CreateGoalAsync(userId, "Goal 2");
            var created = await _stepService.CreateAsync(goal1.Id, new CreateStepRequest("Step"), userId);

            Func<Task> act = async () => await _stepService.MarkCompletedAsync(goal2.Id, created.Id, userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not belong*");
        }

        [Fact]
        public async Task MarkCompletedAsync_PersistsCompletionInRepository()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), userId);

            await _stepService.MarkCompletedAsync(goal.Id, created.Id, userId);

            var step = await _stepRepository.GetByIdAsync(created.Id);
            step.Should().NotBeNull();
            step!.IsCompleted.Should().BeTrue();
            step.CompletedAt.Should().NotBeNull();
        }
    }
}
