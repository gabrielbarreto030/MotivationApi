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
    public class MonthlyReportTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly MonthlyReportService _monthlyReportService;
        private readonly Guid _userId = Guid.NewGuid();

        public MonthlyReportTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_MonthlyReport_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _monthlyReportService = new MonthlyReportService(
                _goalRepository,
                _stepRepository,
                NullLogger<MonthlyReportService>.Instance);
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
        public async Task GetMonthlyReportAsync_NoGoals_ReturnsZeroTotals()
        {
            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
            result.MostActiveDay.Should().BeNull();
            result.MostProductiveWeek.Should().BeNull();
            result.AverageStepsPerDay.Should().Be(0);
        }

        [Fact]
        public async Task GetMonthlyReportAsync_NoGoals_WeeklyBreakdownHasFiveEntries()
        {
            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.WeeklyBreakdown.Should().HaveCount(5);
            result.WeeklyBreakdown.All(w => w.StepsCompleted == 0).Should().BeTrue();
        }

        [Fact]
        public async Task GetMonthlyReportAsync_OnlyPendingSteps_ReturnsZeroTotals()
        {
            var goal = await AddGoalAsync();
            await AddPendingStepAsync(goal.Id);
            await AddPendingStepAsync(goal.Id);

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
            result.MostActiveDay.Should().BeNull();
            result.MostProductiveWeek.Should().BeNull();
        }

        // ── Steps within/outside the window ──────────────────────────────────────

        [Fact]
        public async Task GetMonthlyReportAsync_StepCompletedToday_CountedInReport()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(1);
            result.TotalGoalsProgressed.Should().Be(1);
        }

        [Fact]
        public async Task GetMonthlyReportAsync_StepCompleted29DaysAgo_CountedInReport()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-29));

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(1);
        }

        [Fact]
        public async Task GetMonthlyReportAsync_StepCompleted30DaysAgo_NotCountedInReport()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-30));

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
        }

        // ── Weekly breakdown ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetMonthlyReportAsync_WeeklyBreakdown_AlwaysFiveEntries()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.WeeklyBreakdown.Should().HaveCount(5);
        }

        [Fact]
        public async Task GetMonthlyReportAsync_WeeklyBreakdown_SortedAscendingByWeekNumber()
        {
            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.WeeklyBreakdown.Should().BeInAscendingOrder(w => w.WeekNumber);
            result.WeeklyBreakdown.First().WeekNumber.Should().Be(1);
            result.WeeklyBreakdown.Last().WeekNumber.Should().Be(5);
        }

        [Fact]
        public async Task GetMonthlyReportAsync_WeeklyBreakdown_StepInLastWeekCountedInWeek5()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow); // today = last day of week 5

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.WeeklyBreakdown.Last().StepsCompleted.Should().Be(1);
        }

        [Fact]
        public async Task GetMonthlyReportAsync_WeeklyBreakdown_StepInFirstWeekCountedInWeek1()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-29)); // first day = week 1

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.WeeklyBreakdown.First().StepsCompleted.Should().Be(1);
        }

        // ── MostActiveDay ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetMonthlyReportAsync_MostActiveDay_IsNullWhenNoSteps()
        {
            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.MostActiveDay.Should().BeNull();
        }

        [Fact]
        public async Task GetMonthlyReportAsync_MostActiveDay_CorrectlyIdentified()
        {
            var goal = await AddGoalAsync();
            var twoDaysAgo = DateTime.UtcNow.AddDays(-2);

            // 1 step today, 3 steps two days ago
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal.Id, twoDaysAgo);
            await AddCompletedStepAsync(goal.Id, twoDaysAgo.AddHours(-1));
            await AddCompletedStepAsync(goal.Id, twoDaysAgo.AddHours(-2));

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.MostActiveDay.Should().NotBeNull();
            result.MostActiveDay!.Value.Date.Should().Be(twoDaysAgo.Date);
        }

        [Fact]
        public async Task GetMonthlyReportAsync_MultipleStepsSameDay_AggregatedForMostActiveDay()
        {
            var goal = await AddGoalAsync();
            var today = DateTime.UtcNow;
            await AddCompletedStepAsync(goal.Id, today);
            await AddCompletedStepAsync(goal.Id, today.AddHours(-1));
            await AddCompletedStepAsync(goal.Id, today.AddHours(-3));

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(3);
            result.MostActiveDay!.Value.Date.Should().Be(today.Date);
        }

        // ── MostProductiveWeek ────────────────────────────────────────────────────

        [Fact]
        public async Task GetMonthlyReportAsync_MostProductiveWeek_IsNullWhenNoSteps()
        {
            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.MostProductiveWeek.Should().BeNull();
        }

        [Fact]
        public async Task GetMonthlyReportAsync_MostProductiveWeek_CorrectlyIdentified()
        {
            var goal = await AddGoalAsync();

            // 3 steps 29 days ago (week 1), 1 step today (week 5)
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-29));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-28));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-27));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.MostProductiveWeek.Should().Be(1);
        }

        // ── AverageStepsPerDay ────────────────────────────────────────────────────

        [Fact]
        public async Task GetMonthlyReportAsync_AverageStepsPerDay_ComputedCorrectly()
        {
            var goal = await AddGoalAsync();
            // 30 steps in 30 days = avg 1.0
            for (int i = 0; i < 30; i++)
                await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-i));

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.AverageStepsPerDay.Should().Be(1.0);
        }

        [Fact]
        public async Task GetMonthlyReportAsync_AverageStepsPerDay_RoundedToTwoDecimals()
        {
            var goal = await AddGoalAsync();
            // 1 step in 30 days = 1/30 = 0.03
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.AverageStepsPerDay.Should().Be(Math.Round(1.0 / 30, 2));
        }

        // ── TotalGoalsProgressed ──────────────────────────────────────────────────

        [Fact]
        public async Task GetMonthlyReportAsync_TotalGoalsProgressed_CountsDistinctGoals()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();

            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-1));
            await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow);

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.TotalGoalsProgressed.Should().Be(2);
            result.TotalStepsCompleted.Should().Be(3);
        }

        // ── MonthStart / MonthEnd ─────────────────────────────────────────────────

        [Fact]
        public async Task GetMonthlyReportAsync_MonthStartAndEnd_Cover30Days()
        {
            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            var diff = (result.MonthEnd.Date - result.MonthStart.Date).TotalDays;
            diff.Should().Be(29); // 30 days inclusive
        }

        // ── Data isolation ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetMonthlyReportAsync_OtherUsersSteps_NotIncluded()
        {
            var otherUserId = Guid.NewGuid();
            var otherGoal = new Goal(Guid.NewGuid(), otherUserId, "Other", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(otherGoal);
            await AddCompletedStepAsync(otherGoal.Id, DateTime.UtcNow);

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
        }

        // ── Combined scenario ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetMonthlyReportAsync_CombinedScenario_AllFieldsCorrect()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();

            // goal1: 2 steps today (week 5), 1 step 10 days ago (week 4)
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddHours(-2));
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-10));

            // goal2: 5 steps 25 days ago (week 1) - most productive week
            for (int i = 0; i < 5; i++)
                await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow.AddDays(-25).AddHours(-i));

            // Step outside the window - not counted
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-35));

            var result = await _monthlyReportService.GetMonthlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(8);
            result.TotalGoalsProgressed.Should().Be(2);
            result.WeeklyBreakdown.Should().HaveCount(5);
            result.MostActiveDay.Should().NotBeNull();
            result.MostProductiveWeek.Should().Be(1); // week 1 has 5 steps (most)
            result.AverageStepsPerDay.Should().Be(Math.Round(8.0 / 30, 2));
        }
    }
}
