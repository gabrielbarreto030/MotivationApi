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
    public class WeekdayStatsTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly WeekdayStatsService _service;
        private readonly Guid _userId = Guid.NewGuid();

        public WeekdayStatsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_WeekdayStats_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _service = new WeekdayStatsService(
                _goalRepository,
                _stepRepository,
                NullLogger<WeekdayStatsService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> AddGoalAsync(Guid? userId = null)
        {
            var goal = new Goal(Guid.NewGuid(), userId ?? _userId, "Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
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
            var step = new Step(Guid.NewGuid(), goalId, "Pending");
            await _stepRepository.AddAsync(step);
            return step;
        }

        // Returns the next UTC DateTime that falls on a given DayOfWeek
        private static DateTime NextDay(DayOfWeek target)
        {
            var now = DateTime.UtcNow.Date;
            int diff = ((int)target - (int)now.DayOfWeek + 7) % 7;
            return now.AddDays(diff);
        }

        // ── No goals ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_NoGoals_ReturnsAllZerosAndNullMostProductiveDay()
        {
            var result = await _service.GetWeekdayStatsAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.MostProductiveDay.Should().BeNull();
            result.Entries.Should().HaveCount(7);
            result.Entries.All(e => e.StepsCompleted == 0).Should().BeTrue();
        }

        // ── Only pending steps ─────────────────────────────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_OnlyPendingSteps_ReturnsAllZeros()
        {
            var goal = await AddGoalAsync();
            await AddPendingStepAsync(goal.Id);
            await AddPendingStepAsync(goal.Id);

            var result = await _service.GetWeekdayStatsAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.MostProductiveDay.Should().BeNull();
            result.Entries.All(e => e.StepsCompleted == 0).Should().BeTrue();
        }

        // ── Always 7 entries ordered Monday–Sunday ────────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_AlwaysReturns7EntriesInOrder()
        {
            var result = await _service.GetWeekdayStatsAsync(_userId);

            result.Entries.Should().HaveCount(7);
            result.Entries[0].DayName.Should().Be("Monday");
            result.Entries[1].DayName.Should().Be("Tuesday");
            result.Entries[2].DayName.Should().Be("Wednesday");
            result.Entries[3].DayName.Should().Be("Thursday");
            result.Entries[4].DayName.Should().Be("Friday");
            result.Entries[5].DayName.Should().Be("Saturday");
            result.Entries[6].DayName.Should().Be("Sunday");
        }

        // ── Single completed step counted for correct day ─────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_SingleMondayStep_CountsUnderMonday()
        {
            var goal = await AddGoalAsync();
            var monday = NextDay(DayOfWeek.Monday);
            await AddCompletedStepAsync(goal.Id, monday);

            var result = await _service.GetWeekdayStatsAsync(_userId);

            var mondayEntry = result.Entries.First(e => e.DayName == "Monday");
            mondayEntry.StepsCompleted.Should().Be(1);
            result.TotalStepsCompleted.Should().Be(1);
        }

        [Fact]
        public async Task GetWeekdayStatsAsync_SingleSundayStep_CountsUnderSunday()
        {
            var goal = await AddGoalAsync();
            var sunday = NextDay(DayOfWeek.Sunday);
            await AddCompletedStepAsync(goal.Id, sunday);

            var result = await _service.GetWeekdayStatsAsync(_userId);

            var sundayEntry = result.Entries.First(e => e.DayName == "Sunday");
            sundayEntry.StepsCompleted.Should().Be(1);
            result.TotalStepsCompleted.Should().Be(1);
        }

        // ── Multiple steps on same day aggregated ─────────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_MultipleStepsSameDay_AggregatesCount()
        {
            var goal = await AddGoalAsync();
            var wednesday = NextDay(DayOfWeek.Wednesday);
            await AddCompletedStepAsync(goal.Id, wednesday);
            await AddCompletedStepAsync(goal.Id, wednesday.AddHours(2));
            await AddCompletedStepAsync(goal.Id, wednesday.AddHours(5));

            var result = await _service.GetWeekdayStatsAsync(_userId);

            var wedEntry = result.Entries.First(e => e.DayName == "Wednesday");
            wedEntry.StepsCompleted.Should().Be(3);
            result.TotalStepsCompleted.Should().Be(3);
        }

        // ── Most productive day identified ────────────────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_MostProductiveDay_IsCorrect()
        {
            var goal = await AddGoalAsync();
            var friday = NextDay(DayOfWeek.Friday);
            var tuesday = NextDay(DayOfWeek.Tuesday);

            await AddCompletedStepAsync(goal.Id, tuesday);
            await AddCompletedStepAsync(goal.Id, friday);
            await AddCompletedStepAsync(goal.Id, friday.AddHours(1));
            await AddCompletedStepAsync(goal.Id, friday.AddHours(2));

            var result = await _service.GetWeekdayStatsAsync(_userId);

            result.MostProductiveDay.Should().Be("Friday");
        }

        // ── Steps across multiple goals combined ──────────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_StepsAcrossMultipleGoals_Combined()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();
            var thursday = NextDay(DayOfWeek.Thursday);

            await AddCompletedStepAsync(goal1.Id, thursday);
            await AddCompletedStepAsync(goal2.Id, thursday.AddHours(1));

            var result = await _service.GetWeekdayStatsAsync(_userId);

            var thuEntry = result.Entries.First(e => e.DayName == "Thursday");
            thuEntry.StepsCompleted.Should().Be(2);
            result.TotalStepsCompleted.Should().Be(2);
        }

        // ── Data isolation between users ──────────────────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_DataIsolation_OtherUsersStepsNotCounted()
        {
            var otherUser = Guid.NewGuid();
            var otherGoal = await AddGoalAsync(otherUser);
            var monday = NextDay(DayOfWeek.Monday);
            await AddCompletedStepAsync(otherGoal.Id, monday);

            var result = await _service.GetWeekdayStatsAsync(_userId);

            result.TotalStepsCompleted.Should().Be(0);
            result.MostProductiveDay.Should().BeNull();
        }

        // ── Mixed completed and pending ────────────────────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_MixedSteps_OnlyCompletedCounted()
        {
            var goal = await AddGoalAsync();
            var saturday = NextDay(DayOfWeek.Saturday);
            await AddCompletedStepAsync(goal.Id, saturday);
            await AddPendingStepAsync(goal.Id);

            var result = await _service.GetWeekdayStatsAsync(_userId);

            result.TotalStepsCompleted.Should().Be(1);
            var satEntry = result.Entries.First(e => e.DayName == "Saturday");
            satEntry.StepsCompleted.Should().Be(1);
        }

        // ── TotalStepsCompleted is sum of all entries ──────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_TotalMatchesSumOfEntries()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, NextDay(DayOfWeek.Monday));
            await AddCompletedStepAsync(goal.Id, NextDay(DayOfWeek.Wednesday));
            await AddCompletedStepAsync(goal.Id, NextDay(DayOfWeek.Friday));

            var result = await _service.GetWeekdayStatsAsync(_userId);

            result.TotalStepsCompleted.Should().Be(result.Entries.Sum(e => e.StepsCompleted));
            result.TotalStepsCompleted.Should().Be(3);
        }

        // ── Combined full scenario ─────────────────────────────────────────────

        [Fact]
        public async Task GetWeekdayStatsAsync_FullScenario_ReturnsCorrectData()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();
            var otherUser = Guid.NewGuid();
            var otherGoal = await AddGoalAsync(otherUser);

            var monday = NextDay(DayOfWeek.Monday);
            var tuesday = NextDay(DayOfWeek.Tuesday);
            var friday = NextDay(DayOfWeek.Friday);

            // user steps
            await AddCompletedStepAsync(goal1.Id, monday);
            await AddCompletedStepAsync(goal1.Id, monday.AddHours(1));
            await AddCompletedStepAsync(goal2.Id, tuesday);
            await AddCompletedStepAsync(goal2.Id, friday);
            await AddPendingStepAsync(goal1.Id);

            // other user steps (should be ignored)
            await AddCompletedStepAsync(otherGoal.Id, monday);
            await AddCompletedStepAsync(otherGoal.Id, tuesday);

            var result = await _service.GetWeekdayStatsAsync(_userId);

            result.TotalStepsCompleted.Should().Be(4);
            result.MostProductiveDay.Should().Be("Monday");
            result.Entries.Should().HaveCount(7);
            result.Entries.First(e => e.DayName == "Monday").StepsCompleted.Should().Be(2);
            result.Entries.First(e => e.DayName == "Tuesday").StepsCompleted.Should().Be(1);
            result.Entries.First(e => e.DayName == "Friday").StepsCompleted.Should().Be(1);
            result.Entries.First(e => e.DayName == "Wednesday").StepsCompleted.Should().Be(0);
        }
    }
}
