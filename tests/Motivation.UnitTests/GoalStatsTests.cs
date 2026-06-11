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
    public class GoalStatsTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly Guid _userId = Guid.NewGuid();

        public GoalStatsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_GoalStats_" + Guid.NewGuid())
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

        private async Task<Goal> AddGoalAsync(
            string title = "Goal",
            GoalStatus status = GoalStatus.Pending,
            GoalPriority priority = GoalPriority.None,
            bool archived = false,
            bool pinned = false,
            DateTime? deadline = null,
            string[]? tags = null,
            DateTime? completedAt = null)
        {
            var now = DateTime.UtcNow;
            var goal = new Goal(
                Guid.NewGuid(), _userId, title, "Desc",
                status, now, deadline, priority,
                null, archived, pinned, completedAt,
                tags != null ? string.Join(',', tags) : null);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        // ── Empty user ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_NoGoals_ReturnsZeroStats()
        {
            var stats = await _goalService.GetStatsAsync(_userId);

            stats.TotalGoals.Should().Be(0);
            stats.ArchivedGoals.Should().Be(0);
            stats.PinnedGoals.Should().Be(0);
            stats.OverdueGoals.Should().Be(0);
            stats.GoalsByStatus.Should().BeEmpty();
            stats.GoalsByPriority.Should().BeEmpty();
            stats.TagBreakdown.Should().BeEmpty();
            stats.AvgCompletionDays.Should().BeNull();
        }

        // ── TotalGoals ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_ThreeGoals_TotalIsThree()
        {
            await AddGoalAsync("A");
            await AddGoalAsync("B");
            await AddGoalAsync("C");

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.TotalGoals.Should().Be(3);
        }

        [Fact]
        public async Task GetStatsAsync_IncludesArchivedInTotal()
        {
            await AddGoalAsync("Active");
            await AddGoalAsync("Archived", archived: true);

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.TotalGoals.Should().Be(2);
            stats.ArchivedGoals.Should().Be(1);
        }

        // ── GoalsByStatus ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_MixedStatuses_BreakdownIsCorrect()
        {
            await AddGoalAsync("A", status: GoalStatus.Pending);
            await AddGoalAsync("B", status: GoalStatus.Pending);
            await AddGoalAsync("C", status: GoalStatus.InProgress);
            await AddGoalAsync("D", status: GoalStatus.Completed, completedAt: DateTime.UtcNow);
            await AddGoalAsync("E", status: GoalStatus.Cancelled);

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.GoalsByStatus["Pending"].Should().Be(2);
            stats.GoalsByStatus["InProgress"].Should().Be(1);
            stats.GoalsByStatus["Completed"].Should().Be(1);
            stats.GoalsByStatus["Cancelled"].Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_AllPending_StatusBreakdownHasSingleEntry()
        {
            await AddGoalAsync("A", status: GoalStatus.Pending);
            await AddGoalAsync("B", status: GoalStatus.Pending);

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.GoalsByStatus.Should().HaveCount(1);
            stats.GoalsByStatus["Pending"].Should().Be(2);
        }

        // ── GoalsByPriority ───────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_MixedPriorities_BreakdownIsCorrect()
        {
            await AddGoalAsync("A", priority: GoalPriority.High);
            await AddGoalAsync("B", priority: GoalPriority.High);
            await AddGoalAsync("C", priority: GoalPriority.Medium);
            await AddGoalAsync("D", priority: GoalPriority.None);

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.GoalsByPriority["High"].Should().Be(2);
            stats.GoalsByPriority["Medium"].Should().Be(1);
            stats.GoalsByPriority["None"].Should().Be(1);
        }

        // ── ArchivedGoals / PinnedGoals ───────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_TwoPinnedTwoArchived_CountsCorrectly()
        {
            await AddGoalAsync("P1", pinned: true);
            await AddGoalAsync("P2", pinned: true);
            await AddGoalAsync("A1", archived: true);
            await AddGoalAsync("A2", archived: true);

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.PinnedGoals.Should().Be(2);
            stats.ArchivedGoals.Should().Be(2);
        }

        // ── OverdueGoals ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_TwoOverdueGoals_CountsCorrectly()
        {
            var past = DateTime.UtcNow.AddDays(-1);
            await AddGoalAsync("A", status: GoalStatus.Pending, deadline: past);
            await AddGoalAsync("B", status: GoalStatus.InProgress, deadline: past);
            await AddGoalAsync("C", status: GoalStatus.Pending); // no deadline

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.OverdueGoals.Should().Be(2);
        }

        [Fact]
        public async Task GetStatsAsync_CompletedGoalPastDeadline_NotCountedAsOverdue()
        {
            var past = DateTime.UtcNow.AddDays(-1);
            await AddGoalAsync("A", status: GoalStatus.Completed, deadline: past, completedAt: DateTime.UtcNow);

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.OverdueGoals.Should().Be(0);
        }

        // ── TagBreakdown ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_WithTags_TagBreakdownIsCorrect()
        {
            await AddGoalAsync("A", tags: new[] { "health", "fitness" });
            await AddGoalAsync("B", tags: new[] { "health" });
            await AddGoalAsync("C", tags: new[] { "work" });

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.TagBreakdown["health"].Should().Be(2);
            stats.TagBreakdown["fitness"].Should().Be(1);
            stats.TagBreakdown["work"].Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_NoTags_TagBreakdownIsEmpty()
        {
            await AddGoalAsync("A");
            await AddGoalAsync("B");

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.TagBreakdown.Should().BeEmpty();
        }

        // ── AvgCompletionDays ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_NoCompletedGoals_AvgCompletionDaysIsNull()
        {
            await AddGoalAsync("A", status: GoalStatus.Pending);

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.AvgCompletionDays.Should().BeNull();
        }

        [Fact]
        public async Task GetStatsAsync_TwoCompletedGoals_AvgCompletionDaysIsCorrect()
        {
            var created1 = DateTime.UtcNow.AddDays(-10);
            var completed1 = DateTime.UtcNow.AddDays(-5); // 5 days
            var created2 = DateTime.UtcNow.AddDays(-6);
            var completed2 = DateTime.UtcNow.AddDays(-3); // 3 days

            var goal1 = new Goal(Guid.NewGuid(), _userId, "G1", "Desc", GoalStatus.Completed, created1, null, GoalPriority.None, null, false, false, completed1);
            var goal2 = new Goal(Guid.NewGuid(), _userId, "G2", "Desc", GoalStatus.Completed, created2, null, GoalPriority.None, null, false, false, completed2);
            await _goalRepository.AddAsync(goal1);
            await _goalRepository.AddAsync(goal2);

            var stats = await _goalService.GetStatsAsync(_userId);

            // avg of ~5 and ~3 days ≈ 4 days
            stats.AvgCompletionDays.Should().NotBeNull();
            stats.AvgCompletionDays!.Value.Should().BeApproximately(4.0, 0.5);
        }

        // ── Data isolation ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_OtherUsersGoals_NotIncluded()
        {
            await AddGoalAsync("Mine");

            var otherUserId = Guid.NewGuid();
            var otherGoal = new Goal(Guid.NewGuid(), otherUserId, "Theirs", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(otherGoal);

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.TotalGoals.Should().Be(1);
        }

        // ── Combined scenario ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_FullScenario_AllFieldsCorrect()
        {
            var past = DateTime.UtcNow.AddDays(-2);
            var completedAt = DateTime.UtcNow.AddDays(-1);

            await AddGoalAsync("A", status: GoalStatus.Pending,    priority: GoalPriority.High,   tags: new[] { "work", "urgent" });
            await AddGoalAsync("B", status: GoalStatus.InProgress, priority: GoalPriority.Medium, tags: new[] { "work" });
            await AddGoalAsync("C", status: GoalStatus.Completed,  priority: GoalPriority.Low,    completedAt: completedAt);
            await AddGoalAsync("D", status: GoalStatus.Pending,    priority: GoalPriority.None,   deadline: past); // overdue
            await AddGoalAsync("E", status: GoalStatus.Pending,    archived: true,                tags: new[] { "health" });
            await AddGoalAsync("F", status: GoalStatus.Pending,    pinned: true);

            var stats = await _goalService.GetStatsAsync(_userId);

            stats.TotalGoals.Should().Be(6);
            stats.ArchivedGoals.Should().Be(1);
            stats.PinnedGoals.Should().Be(1);
            stats.OverdueGoals.Should().Be(1);
            stats.GoalsByStatus["Pending"].Should().Be(4);
            stats.GoalsByStatus["InProgress"].Should().Be(1);
            stats.GoalsByStatus["Completed"].Should().Be(1);
            stats.GoalsByPriority["High"].Should().Be(1);
            stats.GoalsByPriority["Medium"].Should().Be(1);
            stats.GoalsByPriority["Low"].Should().Be(1);
            stats.GoalsByPriority["None"].Should().Be(3);
            stats.TagBreakdown["work"].Should().Be(2);
            stats.TagBreakdown["urgent"].Should().Be(1);
            stats.TagBreakdown["health"].Should().Be(1);
            stats.AvgCompletionDays.Should().NotBeNull();
        }
    }
}
