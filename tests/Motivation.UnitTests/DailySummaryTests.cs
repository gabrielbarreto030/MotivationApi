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
    public class DailySummaryTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly DailySummaryService _service;
        private readonly Guid _userId = Guid.NewGuid();

        public DailySummaryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_DailySummary_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _service = new DailySummaryService(
                _goalRepository,
                _stepRepository,
                NullLogger<DailySummaryService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> AddGoalAsync(string title = "Goal")
        {
            var goal = new Goal(Guid.NewGuid(), _userId, title, "Desc", GoalStatus.Pending, DateTime.UtcNow);
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

        private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

        // ── No activity ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_NoGoals_ReturnsTotalStepsZero()
        {
            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.TotalStepsCompleted.Should().Be(0);
            result.GoalsProgressed.Should().Be(0);
            result.Entries.Should().BeEmpty();
        }

        [Fact]
        public async Task GetDailySummaryAsync_NoGoals_ReturnsCorrectDate()
        {
            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.Date.Should().Be(Today);
        }

        [Fact]
        public async Task GetDailySummaryAsync_OnlyPendingSteps_ReturnsTotalStepsZero()
        {
            var goal = await AddGoalAsync();
            await AddPendingStepAsync(goal.Id);
            await AddPendingStepAsync(goal.Id);

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.TotalStepsCompleted.Should().Be(0);
            result.Entries.Should().BeEmpty();
        }

        // ── Single step ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_SingleCompletedStepToday_ReturnsOneEntry()
        {
            var goal = await AddGoalAsync("My Goal");
            var completedAt = DateTime.UtcNow.Date.AddHours(9);
            var step = await AddCompletedStepAsync(goal.Id, completedAt, "My Step");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.TotalStepsCompleted.Should().Be(1);
            result.GoalsProgressed.Should().Be(1);
            result.Entries.Should().HaveCount(1);

            var entry = result.Entries[0];
            entry.GoalId.Should().Be(goal.Id);
            entry.GoalTitle.Should().Be("My Goal");
            entry.Steps.Should().HaveCount(1);
            entry.Steps[0].StepId.Should().Be(step.Id);
            entry.Steps[0].StepTitle.Should().Be("My Step");
            entry.Steps[0].CompletedAt.Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
        }

        // ── Steps on different dates ────────────────────────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_StepCompletedYesterday_NotIncludedForToday()
        {
            var goal = await AddGoalAsync();
            var yesterday = DateTime.UtcNow.AddDays(-1);
            await AddCompletedStepAsync(goal.Id, yesterday, "Yesterday Step");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.TotalStepsCompleted.Should().Be(0);
            result.Entries.Should().BeEmpty();
        }

        [Fact]
        public async Task GetDailySummaryAsync_StepCompletedYesterday_IncludedWhenQueryingYesterday()
        {
            var goal = await AddGoalAsync("Goal");
            var yesterday = DateTime.UtcNow.AddDays(-1);
            await AddCompletedStepAsync(goal.Id, yesterday, "Yesterday Step");

            var yesterdayDate = DateOnly.FromDateTime(yesterday.ToUniversalTime());
            var result = await _service.GetDailySummaryAsync(_userId, yesterdayDate);

            result.TotalStepsCompleted.Should().Be(1);
            result.Entries.Should().HaveCount(1);
            result.Entries[0].Steps[0].StepTitle.Should().Be("Yesterday Step");
        }

        // ── Multiple steps same goal ────────────────────────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_MultipleStepsSameGoal_GroupedUnderOneEntry()
        {
            var goal = await AddGoalAsync("Goal A");
            var today = DateTime.UtcNow.Date;
            await AddCompletedStepAsync(goal.Id, today.AddHours(8), "Step 1");
            await AddCompletedStepAsync(goal.Id, today.AddHours(10), "Step 2");
            await AddCompletedStepAsync(goal.Id, today.AddHours(14), "Step 3");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.TotalStepsCompleted.Should().Be(3);
            result.GoalsProgressed.Should().Be(1);
            result.Entries.Should().HaveCount(1);
            result.Entries[0].Steps.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetDailySummaryAsync_MultipleStepsSameGoal_StepsOrderedByCompletedAtAsc()
        {
            var goal = await AddGoalAsync();
            var today = DateTime.UtcNow.Date;
            await AddCompletedStepAsync(goal.Id, today.AddHours(14), "Afternoon");
            await AddCompletedStepAsync(goal.Id, today.AddHours(8), "Morning");
            await AddCompletedStepAsync(goal.Id, today.AddHours(20), "Evening");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            var steps = result.Entries[0].Steps;
            steps[0].StepTitle.Should().Be("Morning");
            steps[1].StepTitle.Should().Be("Afternoon");
            steps[2].StepTitle.Should().Be("Evening");
        }

        // ── Multiple goals ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_MultipleGoals_EachGroupedSeparately()
        {
            var goal1 = await AddGoalAsync("Goal B");
            var goal2 = await AddGoalAsync("Goal A");
            var today = DateTime.UtcNow.Date;

            await AddCompletedStepAsync(goal1.Id, today.AddHours(9), "Step B1");
            await AddCompletedStepAsync(goal2.Id, today.AddHours(10), "Step A1");
            await AddCompletedStepAsync(goal2.Id, today.AddHours(11), "Step A2");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.TotalStepsCompleted.Should().Be(3);
            result.GoalsProgressed.Should().Be(2);
            result.Entries.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetDailySummaryAsync_MultipleGoals_EntriesOrderedByGoalTitleAsc()
        {
            var goalZ = await AddGoalAsync("Zebra Goal");
            var goalA = await AddGoalAsync("Alpha Goal");
            var goalM = await AddGoalAsync("Middle Goal");
            var today = DateTime.UtcNow.Date;

            await AddCompletedStepAsync(goalZ.Id, today.AddHours(9), "Step Z");
            await AddCompletedStepAsync(goalA.Id, today.AddHours(10), "Step A");
            await AddCompletedStepAsync(goalM.Id, today.AddHours(11), "Step M");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.Entries[0].GoalTitle.Should().Be("Alpha Goal");
            result.Entries[1].GoalTitle.Should().Be("Middle Goal");
            result.Entries[2].GoalTitle.Should().Be("Zebra Goal");
        }

        // ── Mixed completed and pending ─────────────────────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_MixedCompletedAndPending_OnlyCompletedIncluded()
        {
            var goal = await AddGoalAsync();
            var today = DateTime.UtcNow.Date;
            await AddCompletedStepAsync(goal.Id, today.AddHours(9), "Done");
            await AddPendingStepAsync(goal.Id, "Pending");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.TotalStepsCompleted.Should().Be(1);
            result.Entries[0].Steps.Single().StepTitle.Should().Be("Done");
        }

        // ── Data isolation ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_OtherUsersSteps_NotIncluded()
        {
            var otherUserId = Guid.NewGuid();
            var otherGoal = new Goal(Guid.NewGuid(), otherUserId, "Other Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(otherGoal);
            var step = new Step(Guid.NewGuid(), otherGoal.Id, "Other Step");
            step.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.AddAsync(step);

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.TotalStepsCompleted.Should().Be(0);
            result.Entries.Should().BeEmpty();
        }

        // ── GoalsProgressed counts distinct goals ───────────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_TwoStepsSameGoal_GoalsProgressedIsOne()
        {
            var goal = await AddGoalAsync();
            var today = DateTime.UtcNow.Date;
            await AddCompletedStepAsync(goal.Id, today.AddHours(9), "Step 1");
            await AddCompletedStepAsync(goal.Id, today.AddHours(10), "Step 2");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.GoalsProgressed.Should().Be(1);
        }

        // ── Goal with no steps on the date is excluded ──────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_GoalWithNoStepsOnDate_ExcludedFromEntries()
        {
            var goalWithStep = await AddGoalAsync("Goal With Step");
            var goalEmpty = await AddGoalAsync("Empty Goal");
            await AddCompletedStepAsync(goalWithStep.Id, DateTime.UtcNow, "Step");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.Entries.Should().HaveCount(1);
            result.Entries[0].GoalTitle.Should().Be("Goal With Step");
        }

        // ── Combined full scenario ──────────────────────────────────────────────────

        [Fact]
        public async Task GetDailySummaryAsync_CombinedScenario_ReturnsCorrectSummary()
        {
            var goal1 = await AddGoalAsync("Coding");
            var goal2 = await AddGoalAsync("Fitness");
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);

            // Today
            await AddCompletedStepAsync(goal1.Id, today.AddHours(8), "Write tests");
            await AddCompletedStepAsync(goal1.Id, today.AddHours(12), "Review PR");
            await AddCompletedStepAsync(goal2.Id, today.AddHours(7), "Morning run");

            // Yesterday - should not appear for today query
            await AddCompletedStepAsync(goal1.Id, yesterday.AddHours(10), "Old task");

            // Pending - should not appear
            await AddPendingStepAsync(goal2.Id, "Upcoming workout");

            var result = await _service.GetDailySummaryAsync(_userId, Today);

            result.TotalStepsCompleted.Should().Be(3);
            result.GoalsProgressed.Should().Be(2);
            result.Entries.Should().HaveCount(2);

            // Ordered by GoalTitle: "Coding" before "Fitness"
            result.Entries[0].GoalTitle.Should().Be("Coding");
            result.Entries[0].Steps.Should().HaveCount(2);
            result.Entries[1].GoalTitle.Should().Be("Fitness");
            result.Entries[1].Steps.Should().HaveCount(1);
        }
    }
}
