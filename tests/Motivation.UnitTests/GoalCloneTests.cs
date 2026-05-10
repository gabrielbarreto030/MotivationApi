using System;
using System.Linq;
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
    public class GoalCloneTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly StepService _stepService;

        public GoalCloneTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_GoalClone_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _goalService = new GoalService(_goalRepository, _stepRepository, NullLogger<GoalService>.Instance);
            _stepService = new StepService(_stepRepository, _goalRepository, NullLogger<StepService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> CreateGoalAsync(Guid userId, string title = "Original Goal")
        {
            var goal = new Goal(
                Guid.NewGuid(), userId, title, "Description",
                GoalStatus.InProgress, DateTime.UtcNow,
                DateTime.UtcNow.AddDays(30), GoalPriority.High, "Some notes");
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        // Domain: clone creates new goal with "Copy of" prefix

        [Fact]
        public async Task CloneAsync_ReturnsNewGoalWithCopyOfPrefix()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId, "Learn .NET");

            var result = await _goalService.CloneAsync(original.Id, userId);

            result.Title.Should().Be("Copy of Learn .NET");
        }

        [Fact]
        public async Task CloneAsync_ClonedGoalHasNewId()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);

            var result = await _goalService.CloneAsync(original.Id, userId);

            result.Id.Should().NotBe(original.Id);
        }

        [Fact]
        public async Task CloneAsync_ClonedGoalStartsAsPending()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId); // status is InProgress

            var result = await _goalService.CloneAsync(original.Id, userId);

            result.Status.Should().Be(GoalStatus.Pending);
        }

        [Fact]
        public async Task CloneAsync_PreservesDescriptionPriorityNotesAndDeadline()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);

            var result = await _goalService.CloneAsync(original.Id, userId);

            result.Description.Should().Be("Description");
            result.Priority.Should().Be(GoalPriority.High);
            result.Notes.Should().Be("Some notes");
            result.Deadline.Should().NotBeNull();
        }

        [Fact]
        public async Task CloneAsync_ClonedGoalIsNotArchived()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);
            await _goalService.ArchiveAsync(original.Id, userId);

            var result = await _goalService.CloneAsync(original.Id, userId);

            result.IsArchived.Should().BeFalse();
        }

        [Fact]
        public async Task CloneAsync_ClonedGoalAppearsInUserList()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);

            var cloned = await _goalService.CloneAsync(original.Id, userId);

            var all = await _goalService.ListByUserAsync(userId);
            all.Should().Contain(g => g.Id == cloned.Id);
        }

        // Steps cloning

        [Fact]
        public async Task CloneAsync_ClonesAllStepsOfOriginalGoal()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);
            await _stepService.CreateAsync(original.Id, new CreateStepRequest("Step 1"), userId);
            await _stepService.CreateAsync(original.Id, new CreateStepRequest("Step 2"), userId);

            var cloned = await _goalService.CloneAsync(original.Id, userId);

            var clonedSteps = await _stepService.ListByGoalAsync(cloned.Id, userId);
            clonedSteps.Should().HaveCount(2);
            clonedSteps.Select(s => s.Title).Should().Contain(new[] { "Step 1", "Step 2" });
        }

        [Fact]
        public async Task CloneAsync_ClonedStepsAreNotCompleted()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);
            var step = await _stepService.CreateAsync(original.Id, new CreateStepRequest("Step"), userId);
            await _stepService.MarkCompletedAsync(original.Id, step.Id, userId);

            var cloned = await _goalService.CloneAsync(original.Id, userId);

            var clonedSteps = await _stepService.ListByGoalAsync(cloned.Id, userId);
            clonedSteps.Should().AllSatisfy(s =>
            {
                s.IsCompleted.Should().BeFalse();
                s.CompletedAt.Should().BeNull();
            });
        }

        [Fact]
        public async Task CloneAsync_ClonedStepsHaveNewIds()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);
            var step = await _stepService.CreateAsync(original.Id, new CreateStepRequest("Step"), userId);

            var cloned = await _goalService.CloneAsync(original.Id, userId);

            var clonedSteps = await _stepService.ListByGoalAsync(cloned.Id, userId);
            clonedSteps.Should().NotContain(s => s.Id == step.Id);
        }

        [Fact]
        public async Task CloneAsync_ClonedStepsBelongToClonedGoal()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);
            await _stepService.CreateAsync(original.Id, new CreateStepRequest("Step"), userId);

            var cloned = await _goalService.CloneAsync(original.Id, userId);

            var clonedSteps = await _stepService.ListByGoalAsync(cloned.Id, userId);
            clonedSteps.Should().AllSatisfy(s => s.GoalId.Should().Be(cloned.Id));
        }

        [Fact]
        public async Task CloneAsync_NoSteps_ClonesGoalWithNoSteps()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);

            var cloned = await _goalService.CloneAsync(original.Id, userId);

            var clonedSteps = await _stepService.ListByGoalAsync(cloned.Id, userId);
            clonedSteps.Should().BeEmpty();
        }

        [Fact]
        public async Task CloneAsync_OriginalStepsAreUnchanged()
        {
            var userId = Guid.NewGuid();
            var original = await CreateGoalAsync(userId);
            var step = await _stepService.CreateAsync(original.Id, new CreateStepRequest("Step"), userId);
            await _stepService.MarkCompletedAsync(original.Id, step.Id, userId);

            await _goalService.CloneAsync(original.Id, userId);

            var originalSteps = await _stepService.ListByGoalAsync(original.Id, userId);
            originalSteps.Should().Contain(s => s.Id == step.Id && s.IsCompleted);
        }

        // Authorization

        [Fact]
        public async Task CloneAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();

            Func<Task> act = async () => await _goalService.CloneAsync(Guid.NewGuid(), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task CloneAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var original = await CreateGoalAsync(ownerId);

            Func<Task> act = async () => await _goalService.CloneAsync(original.Id, otherId);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }
}
