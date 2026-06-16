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
    public class ActivityHeatmapTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly ActivityHeatmapService _heatmapService;
        private readonly Guid _userId = Guid.NewGuid();

        public ActivityHeatmapTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_ActivityHeatmap_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _heatmapService = new ActivityHeatmapService(
                _goalRepository,
                _stepRepository,
                NullLogger<ActivityHeatmapService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> AddGoalAsync()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        private async Task<Step> AddCompletedStepAsync(Guid goalId, DateTime completedAt)
        {
            var step = new Step(Guid.NewGuid(), goalId, "Step");
            step.MarkCompleted(completedAt);
            await _stepRepository.AddAsync(step);
            return step;
        }

        private async Task<Step> AddPendingStepAsync(Guid goalId)
        {
            var step = new Step(Guid.NewGuid(), goalId, "Pending Step");
            await _stepRepository.AddAsync(step);
            return step;
        }

        // ── No activity ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetHeatmapAsync_NoGoals_ReturnsZeroTotals()
        {
            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.ActiveDays.Should().Be(0);
        }

        [Fact]
        public async Task GetHeatmapAsync_NoGoals_AlwaysReturns365Entries()
        {
            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.Entries.Should().HaveCount(365);
            result.Entries.All(e => e.Count == 0).Should().BeTrue();
        }

        [Fact]
        public async Task GetHeatmapAsync_OnlyPendingSteps_ReturnsZeroTotals()
        {
            var goal = await AddGoalAsync();
            await AddPendingStepAsync(goal.Id);
            await AddPendingStepAsync(goal.Id);

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.ActiveDays.Should().Be(0);
        }

        // ── Window boundaries ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetHeatmapAsync_StepCompletedToday_IsIncluded()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.TotalStepsCompleted.Should().Be(1);
            result.ActiveDays.Should().Be(1);
        }

        [Fact]
        public async Task GetHeatmapAsync_StepCompleted364DaysAgo_IsIncluded()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-364));

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.TotalStepsCompleted.Should().Be(1);
        }

        [Fact]
        public async Task GetHeatmapAsync_StepCompleted365DaysAgo_IsExcluded()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-365));

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.ActiveDays.Should().Be(0);
        }

        // ── Entries structure ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetHeatmapAsync_Entries_AlwaysHas365Items()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.Entries.Should().HaveCount(365);
        }

        [Fact]
        public async Task GetHeatmapAsync_Entries_SortedAscendingByDate()
        {
            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.Entries.Should().BeInAscendingOrder(e => e.Date);
        }

        [Fact]
        public async Task GetHeatmapAsync_Entries_FirstEntryIs364DaysAgo()
        {
            var result = await _heatmapService.GetHeatmapAsync(_userId);

            var expectedStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-364);
            result.Entries.First().Date.Date.Should().Be(expectedStart.ToDateTime(TimeOnly.MinValue).Date);
        }

        [Fact]
        public async Task GetHeatmapAsync_Entries_LastEntryIsToday()
        {
            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.Entries.Last().Date.Date.Should().Be(DateTime.UtcNow.Date);
        }

        [Fact]
        public async Task GetHeatmapAsync_StepCompletedToday_LastEntryHasCountOne()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.Entries.Last().Count.Should().Be(1);
        }

        [Fact]
        public async Task GetHeatmapAsync_StepCompleted364DaysAgo_FirstEntryHasCountOne()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-364));

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.Entries.First().Count.Should().Be(1);
        }

        // ── Multiple steps same day ────────────────────────────────────────────────

        [Fact]
        public async Task GetHeatmapAsync_MultipleStepsSameDay_AggregatedInOneEntry()
        {
            var goal = await AddGoalAsync();
            var today = DateTime.UtcNow;
            await AddCompletedStepAsync(goal.Id, today);
            await AddCompletedStepAsync(goal.Id, today.AddHours(-1));
            await AddCompletedStepAsync(goal.Id, today.AddHours(-3));

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.TotalStepsCompleted.Should().Be(3);
            result.ActiveDays.Should().Be(1);
            result.Entries.Last().Count.Should().Be(3);
        }

        // ── ActiveDays ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetHeatmapAsync_StepsOnDifferentDays_ActiveDaysCountsDistinctDays()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-1));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-2));
            // 2 steps on day -1
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-1).AddHours(-3));

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.ActiveDays.Should().Be(3);
            result.TotalStepsCompleted.Should().Be(4);
        }

        // ── WindowStart / WindowEnd ────────────────────────────────────────────────

        [Fact]
        public async Task GetHeatmapAsync_WindowStartAndEnd_Cover365Days()
        {
            var result = await _heatmapService.GetHeatmapAsync(_userId);

            var diff = (result.WindowEnd.Date - result.WindowStart.Date).TotalDays;
            diff.Should().Be(364); // 365 days inclusive
        }

        // ── Data isolation ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetHeatmapAsync_OtherUsersSteps_NotIncluded()
        {
            var otherUserId = Guid.NewGuid();
            var otherGoal = new Goal(Guid.NewGuid(), otherUserId, "Other", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(otherGoal);
            await AddCompletedStepAsync(otherGoal.Id, DateTime.UtcNow);

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.ActiveDays.Should().Be(0);
        }

        // ── Combined scenario ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetHeatmapAsync_CombinedScenario_AllFieldsCorrect()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();

            // 2 steps today
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddHours(-2));

            // 1 step yesterday
            await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow.AddDays(-1));

            // 3 steps 100 days ago
            for (int i = 0; i < 3; i++)
                await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow.AddDays(-100).AddHours(-i));

            // Step outside window — excluded
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-400));

            var result = await _heatmapService.GetHeatmapAsync(_userId);

            result.TotalStepsCompleted.Should().Be(6);
            result.ActiveDays.Should().Be(3); // today, yesterday, -100 days
            result.Entries.Should().HaveCount(365);
            result.Entries.Last().Count.Should().Be(2); // today
        }
    }
}
