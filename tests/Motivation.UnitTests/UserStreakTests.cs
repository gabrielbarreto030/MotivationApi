using System;
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
    public class UserStreakTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StreakService _streakService;
        private readonly Guid _userId = Guid.NewGuid();

        public UserStreakTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_Streak_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _streakService = new StreakService(_goalRepository, _stepRepository, NullLogger<StreakService>.Instance);
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
        public async Task GetStreakAsync_NoGoals_ReturnsZeroStreak()
        {
            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(0);
            result.LongestStreak.Should().Be(0);
            result.LastActivityDate.Should().BeNull();
        }

        [Fact]
        public async Task GetStreakAsync_GoalWithNoPendingSteps_ReturnsZeroStreak()
        {
            var goal = await AddGoalAsync();
            await AddPendingStepAsync(goal.Id);

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(0);
            result.LongestStreak.Should().Be(0);
            result.LastActivityDate.Should().BeNull();
        }

        // ── Single day ───────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStreakAsync_OneStepCompletedToday_CurrentStreakIsOne()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(1);
            result.LongestStreak.Should().Be(1);
            result.LastActivityDate.Should().NotBeNull();
        }

        [Fact]
        public async Task GetStreakAsync_OneStepCompletedYesterday_CurrentStreakIsOne()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-1));

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(1);
            result.LongestStreak.Should().Be(1);
        }

        [Fact]
        public async Task GetStreakAsync_OneStepCompletedTwoDaysAgo_CurrentStreakIsZero()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-2));

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(0);
            result.LongestStreak.Should().Be(1);
        }

        // ── Consecutive days ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetStreakAsync_ThreeConsecutiveDaysEndingToday_CurrentStreakIsThree()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-1));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-2));

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(3);
            result.LongestStreak.Should().Be(3);
        }

        [Fact]
        public async Task GetStreakAsync_ThreeConsecutiveDaysEndingYesterday_CurrentStreakIsThree()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-1));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-2));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-3));

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(3);
            result.LongestStreak.Should().Be(3);
        }

        // ── Gap breaks streak ────────────────────────────────────────────────────

        [Fact]
        public async Task GetStreakAsync_GapInDays_CurrentStreakCountsOnlyRecentRun()
        {
            var goal = await AddGoalAsync();
            // Recent: today + yesterday = 2
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-1));
            // Gap: 3 days ago (skipped 2 days ago)
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-3));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-4));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-5));

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(2);
            result.LongestStreak.Should().Be(3);
        }

        // ── Multiple completions same day ────────────────────────────────────────

        [Fact]
        public async Task GetStreakAsync_MultipleCompletionsSameDay_CountsAsOneDay()
        {
            var goal = await AddGoalAsync();
            var today = DateTime.UtcNow;
            await AddCompletedStepAsync(goal.Id, today);
            await AddCompletedStepAsync(goal.Id, today.AddHours(-2));
            await AddCompletedStepAsync(goal.Id, today.AddHours(-5));

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(1);
            result.LongestStreak.Should().Be(1);
        }

        // ── Longest streak preserved ─────────────────────────────────────────────

        [Fact]
        public async Task GetStreakAsync_LongestStreakIsLargerThanCurrent()
        {
            var goal = await AddGoalAsync();
            // Old long run: 5 days, 10+ days ago
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-15));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-14));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-13));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-12));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-11));
            // Gap
            // Recent run: 2 days
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddDays(-1));
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(2);
            result.LongestStreak.Should().Be(5);
        }

        // ── LastActivityDate ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetStreakAsync_ReturnsLastActivityDateCorrectly()
        {
            var goal = await AddGoalAsync();
            var threeDaysAgo = DateTime.UtcNow.AddDays(-3);
            var fiveDaysAgo = DateTime.UtcNow.AddDays(-5);
            await AddCompletedStepAsync(goal.Id, threeDaysAgo);
            await AddCompletedStepAsync(goal.Id, fiveDaysAgo);

            var result = await _streakService.GetStreakAsync(_userId);

            result.LastActivityDate.Should().NotBeNull();
            result.LastActivityDate!.Value.Date.Should().Be(threeDaysAgo.Date);
        }

        // ── Data isolation ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStreakAsync_OtherUsersSteps_NotIncluded()
        {
            // Add steps for another user
            var otherUserId = Guid.NewGuid();
            var otherGoal = new Goal(Guid.NewGuid(), otherUserId, "Other", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(otherGoal);
            var otherStep = new Step(Guid.NewGuid(), otherGoal.Id, "Other Step");
            otherStep.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.AddAsync(otherStep);

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(0);
            result.LongestStreak.Should().Be(0);
            result.LastActivityDate.Should().BeNull();
        }

        // ── Multi-goal ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStreakAsync_StepsAcrossMultipleGoals_CombinedCorrectly()
        {
            var goal1 = await AddGoalAsync();
            var goal2 = await AddGoalAsync();

            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow);
            await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow.AddDays(-1));
            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddDays(-2));

            var result = await _streakService.GetStreakAsync(_userId);

            result.CurrentStreak.Should().Be(3);
            result.LongestStreak.Should().Be(3);
        }
    }
}
