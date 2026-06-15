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
    public class YearlyReportTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly YearlyReportService _yearlyReportService;
        private readonly Guid _userId = Guid.NewGuid();

        public YearlyReportTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_YearlyReport_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _yearlyReportService = new YearlyReportService(
                _goalRepository,
                _stepRepository,
                NullLogger<YearlyReportService>.Instance);
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

        // ── No activity ───────────────────────────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_NoGoals_ReturnsZeroTotals()
        {
            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
            result.MostActiveDay.Should().BeNull();
            result.MostProductiveMonth.Should().BeNull();
            result.AverageStepsPerDay.Should().Be(0);
        }

        [Fact]
        public async Task GetYearlyReportAsync_NoGoals_MonthlyBreakdownHasTwelveEntries()
        {
            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.MonthlyBreakdown.Should().HaveCount(12);
            result.MonthlyBreakdown.All(m => m.StepsCompleted == 0).Should().BeTrue();
        }

        [Fact]
        public async Task GetYearlyReportAsync_OnlyPendingSteps_ReturnsZeroTotals()
        {
            var goal = await AddGoalAsync();
            await AddPendingStepAsync(goal.Id);
            await AddPendingStepAsync(goal.Id);

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
            result.MostActiveDay.Should().BeNull();
            result.MostProductiveMonth.Should().BeNull();
        }

        // ── Steps within/outside the window ──────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_StepCompletedToday_CountedInReport()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(1);
            result.TotalGoalsProgressed.Should().Be(1);
        }

        [Fact]
        public async Task GetYearlyReportAsync_StepCompleted364DaysAgo_CountedInReport()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-364));

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(1);
        }

        [Fact]
        public async Task GetYearlyReportAsync_StepCompleted365DaysAgo_NotCountedInReport()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-365));

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
        }

        // ── Monthly breakdown ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_MonthlyBreakdown_AlwaysTwelveEntries()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.MonthlyBreakdown.Should().HaveCount(12);
        }

        [Fact]
        public async Task GetYearlyReportAsync_MonthlyBreakdown_SortedAscendingByMonthNumber()
        {
            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.MonthlyBreakdown.Should().BeInAscendingOrder(m => m.MonthNumber);
            result.MonthlyBreakdown.First().MonthNumber.Should().Be(1);
            result.MonthlyBreakdown.Last().MonthNumber.Should().Be(12);
        }

        [Fact]
        public async Task GetYearlyReportAsync_MonthlyBreakdown_StepTodayInMonth12()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.MonthlyBreakdown.Last().StepsCompleted.Should().Be(1);
        }

        [Fact]
        public async Task GetYearlyReportAsync_MonthlyBreakdown_StepIn364DaysAgoInMonth1()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-364));

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.MonthlyBreakdown.First().StepsCompleted.Should().Be(1);
        }

        // ── MostActiveDay ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_MostActiveDay_IsNullWhenNoSteps()
        {
            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.MostActiveDay.Should().BeNull();
        }

        [Fact]
        public async Task GetYearlyReportAsync_MostActiveDay_CorrectlyIdentified()
        {
            var goal = await AddGoalAsync();
            var twoDaysAgo = DateTime.UtcNow.AddDays(-2);

            // 1 step today, 3 steps two days ago
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal.Id, twoDaysAgo);
            await AddCompletedStepAsync(goal.Id, twoDaysAgo.AddHours(-1));
            await AddCompletedStepAsync(goal.Id, twoDaysAgo.AddHours(-2));

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.MostActiveDay.Should().NotBeNull();
            result.MostActiveDay!.Value.Date.Should().Be(twoDaysAgo.Date);
        }

        [Fact]
        public async Task GetYearlyReportAsync_MultipleStepsSameDay_AggregatedForMostActiveDay()
        {
            var goal = await AddGoalAsync();
            var today = DateTime.UtcNow;
            await AddCompletedStepAsync(goal.Id, today);
            await AddCompletedStepAsync(goal.Id, today.AddHours(-1));
            await AddCompletedStepAsync(goal.Id, today.AddHours(-3));

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(3);
            result.MostActiveDay!.Value.Date.Should().Be(today.Date);
        }

        // ── MostProductiveMonth ───────────────────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_MostProductiveMonth_IsNullWhenNoSteps()
        {
            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.MostProductiveMonth.Should().BeNull();
        }

        [Fact]
        public async Task GetYearlyReportAsync_MostProductiveMonth_CorrectlyIdentified()
        {
            var goal = await AddGoalAsync();

            // 3 steps 364 days ago (month 1), 1 step today (month 12)
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-364));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-363));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-362));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.MostProductiveMonth.Should().Be(1);
        }

        // ── AverageStepsPerDay ────────────────────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_AverageStepsPerDay_ComputedCorrectly()
        {
            var goal = await AddGoalAsync();
            // 365 steps in 365 days = avg 1.0
            for (int i = 0; i < 365; i++)
                await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-i));

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.AverageStepsPerDay.Should().Be(1.0);
        }

        [Fact]
        public async Task GetYearlyReportAsync_AverageStepsPerDay_RoundedToTwoDecimals()
        {
            var goal = await AddGoalAsync();
            // 1 step in 365 days = 1/365 ≈ 0.0
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.AverageStepsPerDay.Should().Be(Math.Round(1.0 / 365, 2));
        }

        // ── TotalGoalsProgressed ──────────────────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_TotalGoalsProgressed_CountsDistinctGoals()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();

            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-1));
            await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow);

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.TotalGoalsProgressed.Should().Be(2);
            result.TotalStepsCompleted.Should().Be(3);
        }

        // ── YearStart / YearEnd ───────────────────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_YearStartAndEnd_Cover365Days()
        {
            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            var diff = (result.YearEnd.Date - result.YearStart.Date).TotalDays;
            diff.Should().Be(364); // 365 days inclusive
        }

        // ── Data isolation ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_OtherUsersSteps_NotIncluded()
        {
            var otherUserId = Guid.NewGuid();
            var otherGoal = new Goal(Guid.NewGuid(), otherUserId, "Other", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(otherGoal);
            await AddCompletedStepAsync(otherGoal.Id, DateTime.UtcNow);

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.TotalGoalsProgressed.Should().Be(0);
        }

        // ── Combined scenario ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetYearlyReportAsync_CombinedScenario_AllFieldsCorrect()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();

            // goal1: 2 steps today (month 12), 1 step 180 days ago (month ~7)
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddHours(-2));
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-180));

            // goal2: 5 steps 350 days ago (month 1) - most productive month
            for (int i = 0; i < 5; i++)
                await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow.AddDays(-350).AddHours(-i));

            // Step outside the window - not counted
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-370));

            var result = await _yearlyReportService.GetYearlyReportAsync(_userId);

            result.TotalStepsCompleted.Should().Be(8);
            result.TotalGoalsProgressed.Should().Be(2);
            result.MonthlyBreakdown.Should().HaveCount(12);
            result.MostActiveDay.Should().NotBeNull();
            result.MostProductiveMonth.Should().Be(1); // month 1 has 5 steps (most)
            result.AverageStepsPerDay.Should().Be(Math.Round(8.0 / 365, 2));
        }
    }
}
