using System;
using System.Linq;
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
    public class GoalTimelineTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalTimelineService _service;
        private readonly Guid _userId = Guid.NewGuid();

        public GoalTimelineTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_GoalTimeline_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _service = new GoalTimelineService(
                _goalRepository,
                _stepRepository,
                NullLogger<GoalTimelineService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> AddGoalAsync(Guid? userId = null, string title = "Goal")
        {
            var goal = new Goal(Guid.NewGuid(), userId ?? _userId, title, "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        private async Task<Step> AddCompletedStepAsync(Guid goalId, DateTime completedAt, string title = "Step")
        {
            var step = new Step(Guid.NewGuid(), goalId, title);
            step.MarkCompleted(completedAt);
            await _stepRepository.AddAsync(step);
            return step;
        }

        private async Task<Step> AddPendingStepAsync(Guid goalId, string title = "Pending Step")
        {
            var step = new Step(Guid.NewGuid(), goalId, title);
            await _stepRepository.AddAsync(step);
            return step;
        }

        // ── Goal not found ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetTimelineAsync_GoalNotFound_ReturnsNull()
        {
            var result = await _service.GetTimelineAsync(_userId, Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetTimelineAsync_GoalBelongsToOtherUser_ReturnsNull()
        {
            var otherUserId = Guid.NewGuid();
            var goal = await AddGoalAsync(otherUserId);

            var result = await _service.GetTimelineAsync(_userId, goal.Id);

            result.Should().BeNull();
        }

        // ── No steps ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetTimelineAsync_NoSteps_ReturnsEmptyEntries()
        {
            var goal = await AddGoalAsync();

            var result = await _service.GetTimelineAsync(_userId, goal.Id);

            result.Should().NotBeNull();
            result!.Entries.Should().BeEmpty();
            result.TotalSteps.Should().Be(0);
            result.CompletedSteps.Should().Be(0);
        }

        // ── Pending steps excluded ────────────────────────────────────────────────

        [Fact]
        public async Task GetTimelineAsync_OnlyPendingSteps_ReturnsEmptyEntries()
        {
            var goal = await AddGoalAsync();
            await AddPendingStepAsync(goal.Id, "Pending 1");
            await AddPendingStepAsync(goal.Id, "Pending 2");

            var result = await _service.GetTimelineAsync(_userId, goal.Id);

            result!.Entries.Should().BeEmpty();
            result.TotalSteps.Should().Be(2);
            result.CompletedSteps.Should().Be(0);
        }

        // ── Single completed step ─────────────────────────────────────────────────

        [Fact]
        public async Task GetTimelineAsync_SingleCompletedStep_ReturnsOneEntry()
        {
            var goal = await AddGoalAsync();
            var completedAt = DateTime.UtcNow.AddHours(-1);
            var step = await AddCompletedStepAsync(goal.Id, completedAt, "Step A");

            var result = await _service.GetTimelineAsync(_userId, goal.Id);

            result!.Entries.Should().HaveCount(1);
            result.Entries[0].StepId.Should().Be(step.Id);
            result.Entries[0].StepTitle.Should().Be("Step A");
            result.Entries[0].CompletedAt.Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
        }

        // ── TotalSteps counts all (pending + completed) ───────────────────────────

        [Fact]
        public async Task GetTimelineAsync_MixedSteps_TotalStepsCountsAll()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddHours(-2));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddHours(-1));
            await AddPendingStepAsync(goal.Id);

            var result = await _service.GetTimelineAsync(_userId, goal.Id);

            result!.TotalSteps.Should().Be(3);
            result.CompletedSteps.Should().Be(2);
            result.Entries.Should().HaveCount(2);
        }

        // ── Ordered by CompletedAt asc ────────────────────────────────────────────

        [Fact]
        public async Task GetTimelineAsync_MultipleCompletedSteps_OrderedByCompletedAtAsc()
        {
            var goal = await AddGoalAsync();
            var t1 = DateTime.UtcNow.AddHours(-3);
            var t2 = DateTime.UtcNow.AddHours(-2);
            var t3 = DateTime.UtcNow.AddHours(-1);

            // Add in reverse order to verify sorting
            await AddCompletedStepAsync(goal.Id, t3, "Step C");
            await AddCompletedStepAsync(goal.Id, t1, "Step A");
            await AddCompletedStepAsync(goal.Id, t2, "Step B");

            var result = await _service.GetTimelineAsync(_userId, goal.Id);

            result!.Entries.Should().HaveCount(3);
            result.Entries[0].StepTitle.Should().Be("Step A");
            result.Entries[1].StepTitle.Should().Be("Step B");
            result.Entries[2].StepTitle.Should().Be("Step C");
        }

        // ── GoalId and GoalTitle correctly mapped ─────────────────────────────────

        [Fact]
        public async Task GetTimelineAsync_ReturnsCorrectGoalMetadata()
        {
            var goal = await AddGoalAsync(title: "My Special Goal");

            var result = await _service.GetTimelineAsync(_userId, goal.Id);

            result!.GoalId.Should().Be(goal.Id);
            result.GoalTitle.Should().Be("My Special Goal");
        }

        // ── Data isolation between users ──────────────────────────────────────────

        [Fact]
        public async Task GetTimelineAsync_DataIsolation_DoesNotReturnOtherUsersGoal()
        {
            var otherUser = Guid.NewGuid();
            var otherGoal = await AddGoalAsync(otherUser, "Other Goal");
            await AddCompletedStepAsync(otherGoal.Id, DateTime.UtcNow.AddHours(-1));

            var result = await _service.GetTimelineAsync(_userId, otherGoal.Id);

            result.Should().BeNull();
        }

        // ── Combined full scenario ────────────────────────────────────────────────

        [Fact]
        public async Task GetTimelineAsync_FullScenario_ReturnsCorrectData()
        {
            var goal = await AddGoalAsync(title: "Full Goal");
            var t1 = DateTime.UtcNow.AddDays(-3);
            var t2 = DateTime.UtcNow.AddDays(-2);
            var t3 = DateTime.UtcNow.AddDays(-1);

            await AddCompletedStepAsync(goal.Id, t2, "Middle Step");
            await AddCompletedStepAsync(goal.Id, t1, "First Step");
            await AddCompletedStepAsync(goal.Id, t3, "Last Step");
            await AddPendingStepAsync(goal.Id, "Not done yet");

            var result = await _service.GetTimelineAsync(_userId, goal.Id);

            result.Should().NotBeNull();
            result!.GoalId.Should().Be(goal.Id);
            result.GoalTitle.Should().Be("Full Goal");
            result.TotalSteps.Should().Be(4);
            result.CompletedSteps.Should().Be(3);
            result.Entries.Should().HaveCount(3);
            result.Entries[0].StepTitle.Should().Be("First Step");
            result.Entries[1].StepTitle.Should().Be("Middle Step");
            result.Entries[2].StepTitle.Should().Be("Last Step");
        }
    }
}
