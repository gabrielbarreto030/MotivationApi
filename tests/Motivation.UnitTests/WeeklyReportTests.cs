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
    public class WeeklyReportTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly WeeklyReportService _weeklyReportService;
        private readonly Guid _userId = Guid.NewGuid();

        public WeeklyReportTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_WeeklyReport_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _weeklyReportService = new WeeklyReportService(
                _goalRepository,
                _stepRepository,
                NullLogger<WeeklyReportService>.Instance);
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

        // ── No activity ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetWeeklyReportAsync_NoGoals_ReturnsZeroTotals()
        {
            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
            result.MostActiveDay.Should().BeNull();
            result.AverageStepsPerDay.Should().Be(0);
        }

        [Fact]
        public async Task GetWeeklyReportAsync_NoGoals_DailyBreakdownHasSevenEntries()
        {
            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.DailyBreakdown.Should().HaveCount(7);
            result.DailyBreakdown.All(d => d.StepsCompleted == 0).Should().BeTrue();
        }

        [Fact]
        public async Task GetWeeklyReportAsync_OnlyPendingSteps_ReturnsZeroTotals()
        {
            var goal = await AddGoalAsync();
            await AddPendingStepAsync(goal.Id);
            await AddPendingStepAsync(goal.Id);

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
            result.MostActiveDay.Should().BeNull();
        }

        // ── Steps within the week ────────────────────────────────────────────────

        [Fact]
        public async Task GetWeeklyReportAsync_StepCompletedToday_CountedInReport()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(1);
            result.TotalGoalsProgressed.Should().Be(1);
        }

        [Fact]
        public async Task GetWeeklyReportAsync_StepCompletedSixDaysAgo_CountedInReport()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-6));

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(1);
        }

        [Fact]
        public async Task GetWeeklyReportAsync_StepCompletedSevenDaysAgo_NotCountedInReport()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-7));

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
        }

        // ── Daily breakdown accuracy ──────────────────────────────────────────────

        [Fact]
        public async Task GetWeeklyReportAsync_DailyBreakdown_AlwaysSevenEntries()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-3));

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.DailyBreakdown.Should().HaveCount(7);
        }

        [Fact]
        public async Task GetWeeklyReportAsync_DailyBreakdown_SortedAscending()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.DailyBreakdown.Should().BeInAscendingOrder(d => d.Date);
        }

        [Fact]
        public async Task GetWeeklyReportAsync_MultipleStepsSameDay_AggregatedInBreakdown()
        {
            var goal = await AddGoalAsync();
            var today = DateTime.UtcNow;
            await AddCompletedStepAsync(goal.Id, today);
            await AddCompletedStepAsync(goal.Id, today.AddHours(-1));
            await AddCompletedStepAsync(goal.Id, today.AddHours(-3));

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            var todayEntry = result.DailyBreakdown.Last();
            todayEntry.StepsCompleted.Should().Be(3);
            result.TotalStepsCompleted.Should().Be(3);
        }

        // ── MostActiveDay ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetWeeklyReportAsync_MostActiveDay_IsNullWhenNoSteps()
        {
            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.MostActiveDay.Should().BeNull();
        }

        [Fact]
        public async Task GetWeeklyReportAsync_MostActiveDay_CorrectlyIdentified()
        {
            var goal = await AddGoalAsync();
            var yesterday = DateTime.UtcNow.AddDays(-1);
            var twoDaysAgo = DateTime.UtcNow.AddDays(-2);

            // 1 step yesterday, 3 steps two days ago
            await AddCompletedStepAsync(goal.Id, yesterday);
            await AddCompletedStepAsync(goal.Id, twoDaysAgo);
            await AddCompletedStepAsync(goal.Id, twoDaysAgo.AddHours(-1));
            await AddCompletedStepAsync(goal.Id, twoDaysAgo.AddHours(-2));

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.MostActiveDay.Should().NotBeNull();
            result.MostActiveDay!.Value.Date.Should().Be(twoDaysAgo.Date);
        }

        // ── AverageStepsPerDay ────────────────────────────────────────────────────

        [Fact]
        public async Task GetWeeklyReportAsync_AverageStepsPerDay_ComputedCorrectly()
        {
            var goal = await AddGoalAsync();
            // Add 7 steps total across 7 days = avg 1.0
            for (int i = 0; i < 7; i++)
                await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-i));

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.AverageStepsPerDay.Should().Be(1.0);
        }

        [Fact]
        public async Task GetWeeklyReportAsync_AverageStepsPerDay_RoundedToTwoDecimals()
        {
            var goal = await AddGoalAsync();
            // 1 step in 7 days = 1/7 = 0.14
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.AverageStepsPerDay.Should().Be(0.14);
        }

        // ── TotalGoalsProgressed ──────────────────────────────────────────────────

        [Fact]
        public async Task GetWeeklyReportAsync_TotalGoalsProgressed_CountsDistinctGoals()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();

            // Multiple steps on goal1, one on goal2
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-1));
            await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow);

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.TotalGoalsProgressed.Should().Be(2);
            result.TotalStepsCompleted.Should().Be(3);
        }

        // ── WeekStart / WeekEnd ───────────────────────────────────────────────────

        [Fact]
        public async Task GetWeeklyReportAsync_WeekStartAndEnd_CoverSevenDays()
        {
            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            var diff = (result.WeekEnd.Date - result.WeekStart.Date).TotalDays;
            diff.Should().Be(6); // 7 days inclusive
        }

        // ── Data isolation ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetWeeklyReportAsync_OtherUsersSteps_NotIncluded()
        {
            var otherUserId = Guid.NewGuid();
            var otherGoal = new Goal(Guid.NewGuid(), otherUserId, "Other", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(otherGoal);
            await AddCompletedStepAsync(otherGoal.Id, DateTime.UtcNow);

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
        }

        // ── Combined scenario ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetWeeklyReportAsync_CombinedScenario_AllFieldsCorrect()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();

            // goal1: 2 steps today, 1 step yesterday
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddHours(-2));
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-1));

            // goal2: 1 step 5 days ago
            await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow.AddDays(-5));

            // Old step outside the window - should not be counted
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-8));

            var result = await _weeklyReportService.GetWeeklyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(4);
            result.TotalGoalsProgressed.Should().Be(2);
            result.DailyBreakdown.Should().HaveCount(7);
            result.DailyBreakdown.Last().StepsCompleted.Should().Be(2); // today: 2 steps
            result.MostActiveDay.Should().NotBeNull();
            result.MostActiveDay!.Value.Date.Should().Be(DateTime.UtcNow.Date);
            result.AverageStepsPerDay.Should().Be(Math.Round(4 / 7.0, 2));
        }
    }
}
