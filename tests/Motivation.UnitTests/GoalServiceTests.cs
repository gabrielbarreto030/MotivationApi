using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Motivation.Application.DTOs;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Domain.Interfaces;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;
using Motivation.Application.Interfaces;

namespace Motivation.UnitTests
{
    public class GoalServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;

        public GoalServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Goal_" + Guid.NewGuid())
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

        private async Task<Goal> CreateGoalAsync(Guid userId, string title = "Goal", string description = "Desc")
        {
            var goal = new Goal(Guid.NewGuid(), userId, title, description, GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        private async Task<Step> CreateStepAsync(Guid goalId, string title = "Step")
        {
            var step = new Step(Guid.NewGuid(), goalId, title);
            await _stepRepository.AddAsync(step);
            return step;
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_CreatesGoal()
        {
            var userId = Guid.NewGuid();
            var request = new CreateGoalRequest("Test Goal", "Test Description");

            var result = await _goalService.CreateAsync(request, userId);

            result.Id.Should().NotBeEmpty();
            result.Title.Should().Be("Test Goal");
            result.Description.Should().Be("Test Description");
            result.Status.Should().Be(GoalStatus.Pending);
            result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

            var stored = await _goalRepository.GetByUserAsync(userId);
            stored.Should().HaveCount(1);
            stored[0].Title.Should().Be("Test Goal");
        }

        [Fact]
        public async Task CreateAsync_EmptyTitle_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var request = new CreateGoalRequest("", "Description");

            Func<Task> act = async () => await _goalService.CreateAsync(request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Title*");
        }

        [Fact]
        public async Task CreateAsync_EmptyDescription_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var request = new CreateGoalRequest("Title", "");

            Func<Task> act = async () => await _goalService.CreateAsync(request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Description*");
        }

        [Fact]
        public async Task ListByUserAsync_ReturnsGoals()
        {
            var userId = Guid.NewGuid();
            // create two goals via repository directly
            var g1 = new Goal(Guid.NewGuid(), userId, "A", "desc", GoalStatus.Pending, DateTime.UtcNow);
            var g2 = new Goal(Guid.NewGuid(), userId, "B", "desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(g1);
            await _goalRepository.AddAsync(g2);

            var results = await _goalService.ListByUserAsync(userId);
            results.Should().HaveCount(2);
            results.Select(r => r.Title).Should().Contain(new[] {"A","B"});
        }

        [Fact]
        public async Task UpdateAsync_ValidRequest_UpdatesGoal()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Original Title", "Original Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var request = new UpdateGoalRequest("Updated Title", "Updated Description", "InProgress");
            var result = await _goalService.UpdateAsync(goal.Id, request, userId);

            result.Title.Should().Be("Updated Title");
            result.Description.Should().Be("Updated Description");
            result.Status.Should().Be(GoalStatus.InProgress);

            // Verify it was persisted
            var updated = await _goalRepository.GetByIdAsync(goal.Id);
            updated.Should().NotBeNull();
            updated!.Title.Should().Be("Updated Title");
            updated.Description.Should().Be("Updated Description");
        }

        [Fact]
        public async Task UpdateAsync_PartialUpdate_UpdatesOnlyProvidedFields()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Original Title", "Original Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            // Only update title
            var request = new UpdateGoalRequest("New Title", null, null);
            var result = await _goalService.UpdateAsync(goal.Id, request, userId);

            result.Title.Should().Be("New Title");
            result.Description.Should().Be("Original Description");
            result.Status.Should().Be(GoalStatus.Pending);
        }

        [Fact]
        public async Task UpdateAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var request = new UpdateGoalRequest("Title", "Description", null);

            Func<Task> act = async () => await _goalService.UpdateAsync(Guid.NewGuid(), request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task UpdateAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Title", "Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var request = new UpdateGoalRequest("New Title", null, null);

            Func<Task> act = async () => await _goalService.UpdateAsync(goal.Id, request, otherUserId);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task UpdateAsync_InvalidStatus_IgnoresStatus()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Title", "Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var request = new UpdateGoalRequest(null, null, "InvalidStatus");
            var result = await _goalService.UpdateAsync(goal.Id, request, userId);

            result.Status.Should().Be(GoalStatus.Pending); // Should remain unchanged
        }

        [Fact]
        public async Task DeleteAsync_ValidRequest_RemovesGoal()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            await _goalService.DeleteAsync(goal.Id, userId);

            var stored = await _goalRepository.GetByUserAsync(userId);
            stored.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            Func<Task> act = async () => await _goalService.DeleteAsync(Guid.NewGuid(), userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task DeleteAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var owner = Guid.NewGuid();
            var other = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), owner, "T", "D", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            Func<Task> act = async () => await _goalService.DeleteAsync(goal.Id, other);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        // GetProgressAsync tests

        [Fact]
        public async Task GetProgressAsync_NoSteps_ReturnsZeroProgress()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var result = await _goalService.GetProgressAsync(goal.Id, userId);

            result.GoalId.Should().Be(goal.Id);
            result.TotalSteps.Should().Be(0);
            result.CompletedSteps.Should().Be(0);
            result.ProgressPercentage.Should().Be(0);
        }

        [Fact]
        public async Task GetProgressAsync_AllStepsCompleted_Returns100Percent()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var step1 = await CreateStepAsync(goal.Id, "Step 1");
            var step2 = await CreateStepAsync(goal.Id, "Step 2");
            step1.MarkCompleted(DateTime.UtcNow);
            step2.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.UpdateAsync(step1);
            await _stepRepository.UpdateAsync(step2);

            var result = await _goalService.GetProgressAsync(goal.Id, userId);

            result.TotalSteps.Should().Be(2);
            result.CompletedSteps.Should().Be(2);
            result.ProgressPercentage.Should().Be(100);
        }

        [Fact]
        public async Task GetProgressAsync_HalfCompleted_Returns50Percent()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var step1 = await CreateStepAsync(goal.Id, "Step 1");
            var step2 = await CreateStepAsync(goal.Id, "Step 2");
            step1.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.UpdateAsync(step1);

            var result = await _goalService.GetProgressAsync(goal.Id, userId);

            result.TotalSteps.Should().Be(2);
            result.CompletedSteps.Should().Be(1);
            result.ProgressPercentage.Should().Be(50);
        }

        [Fact]
        public async Task GetProgressAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();

            Func<Task> act = async () => await _goalService.GetProgressAsync(Guid.NewGuid(), userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task GetProgressAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var owner = Guid.NewGuid();
            var other = Guid.NewGuid();
            var goal = await CreateGoalAsync(owner);

            Func<Task> act = async () => await _goalService.GetProgressAsync(goal.Id, other);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task GetProgressAsync_PartialCompletion_CorrectPercentage()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var step1 = await CreateStepAsync(goal.Id, "Step 1");
            await CreateStepAsync(goal.Id, "Step 2");
            await CreateStepAsync(goal.Id, "Step 3");
            step1.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.UpdateAsync(step1);

            var result = await _goalService.GetProgressAsync(goal.Id, userId);

            result.TotalSteps.Should().Be(3);
            result.CompletedSteps.Should().Be(1);
            result.ProgressPercentage.Should().Be(33.33);
        }
    }
}