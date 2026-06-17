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
    public class RecentActivityTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly RecentActivityService _service;
        private readonly Guid _userId = Guid.NewGuid();

        public RecentActivityTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_RecentActivity_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _service = new RecentActivityService(
                _goalRepository,
                _stepRepository,
                NullLogger<RecentActivityService>.Instance);
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

        // ── No activity ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetRecentActivityAsync_NoGoals_ReturnsTotalCountZero()
        {
            var result = await _service.GetRecentActivityAsync(_userId, 1, 20);

            result.TotalCount.Should().Be(0);
            result.Entries.Should().BeEmpty();
        }

        [Fact]
        public async Task GetRecentActivityAsync_NoGoals_ReturnsPageAndPageSize()
        {
            var result = await _service.GetRecentActivityAsync(_userId, 1, 20);

            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
        }

        [Fact]
        public async Task GetRecentActivityAsync_OnlyPendingSteps_ReturnsTotalCountZero()
        {
            var goal = await AddGoalAsync();
            await AddPendingStepAsync(goal.Id);
            await AddPendingStepAsync(goal.Id);

            var result = await _service.GetRecentActivityAsync(_userId, 1, 20);

            result.TotalCount.Should().Be(0);
            result.Entries.Should().BeEmpty();
        }

        // ── Entry fields ───────────────────────────────────────────────────────────

        [Fact]
        public async Task GetRecentActivityAsync_SingleCompletedStep_EntryHasCorrectFields()
        {
            var goal = await AddGoalAsync("My Goal");
            var completedAt = DateTime.UtcNow.AddHours(-1);
            var step = await AddCompletedStepAsync(goal.Id, completedAt, "My Step");

            var result = await _service.GetRecentActivityAsync(_userId, 1, 20);

            result.TotalCount.Should().Be(1);
            var entry = result.Entries.Single();
            entry.StepId.Should().Be(step.Id);
            entry.StepTitle.Should().Be("My Step");
            entry.GoalId.Should().Be(goal.Id);
            entry.GoalTitle.Should().Be("My Goal");
            entry.CompletedAt.Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
        }

        // ── Ordering ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetRecentActivityAsync_MultipleSteps_OrderedByCompletedAtDesc()
        {
            var goal = await AddGoalAsync();
            var now = DateTime.UtcNow;
            await AddCompletedStepAsync(goal.Id, now.AddDays(-2), "Oldest");
            await AddCompletedStepAsync(goal.Id, now.AddDays(-1), "Middle");
            await AddCompletedStepAsync(goal.Id, now, "Newest");

            var result = await _service.GetRecentActivityAsync(_userId, 1, 20);

            result.Entries[0].StepTitle.Should().Be("Newest");
            result.Entries[1].StepTitle.Should().Be("Middle");
            result.Entries[2].StepTitle.Should().Be("Oldest");
        }

        [Fact]
        public async Task GetRecentActivityAsync_StepsAcrossMultipleGoals_OrderedByCompletedAtDesc()
        {
            var goal1 = await AddGoalAsync("Goal 1");
            var goal2 = await AddGoalAsync("Goal 2");
            var now = DateTime.UtcNow;

            await AddCompletedStepAsync(goal1.Id, now.AddHours(-3), "Step A");
            await AddCompletedStepAsync(goal2.Id, now.AddHours(-1), "Step B");
            await AddCompletedStepAsync(goal1.Id, now.AddHours(-2), "Step C");

            var result = await _service.GetRecentActivityAsync(_userId, 1, 20);

            result.TotalCount.Should().Be(3);
            result.Entries[0].StepTitle.Should().Be("Step B");
            result.Entries[1].StepTitle.Should().Be("Step C");
            result.Entries[2].StepTitle.Should().Be("Step A");
        }

        // ── Pagination ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetRecentActivityAsync_Pagination_TotalCountIsAllSteps()
        {
            var goal = await AddGoalAsync();
            var now = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
                await AddCompletedStepAsync(goal.Id, now.AddHours(-i));

            var result = await _service.GetRecentActivityAsync(_userId, 1, 2);

            result.TotalCount.Should().Be(5);
            result.Entries.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetRecentActivityAsync_SecondPage_ReturnsCorrectEntries()
        {
            var goal = await AddGoalAsync();
            var now = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
                await AddCompletedStepAsync(goal.Id, now.AddHours(-i), $"Step {i}");

            var page1 = await _service.GetRecentActivityAsync(_userId, 1, 3);
            var page2 = await _service.GetRecentActivityAsync(_userId, 2, 3);

            page1.Entries.Should().HaveCount(3);
            page2.Entries.Should().HaveCount(2);
            page2.Page.Should().Be(2);
            // Pages should not overlap
            var p1Ids = page1.Entries.Select(e => e.StepId).ToHashSet();
            page2.Entries.Select(e => e.StepId).Should().NotIntersectWith(p1Ids);
        }

        [Fact]
        public async Task GetRecentActivityAsync_PageBeyondData_ReturnsEmptyEntries()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow);

            var result = await _service.GetRecentActivityAsync(_userId, 99, 20);

            result.TotalCount.Should().Be(1);
            result.Entries.Should().BeEmpty();
        }

        [Fact]
        public async Task GetRecentActivityAsync_PageSizeZeroOrNegative_ClampsToOne()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow, "Step");

            var result = await _service.GetRecentActivityAsync(_userId, 1, 0);

            result.PageSize.Should().Be(1);
            result.Entries.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetRecentActivityAsync_PageSizeOver100_ClampsTo100()
        {
            var goal = await AddGoalAsync();
            for (int i = 0; i < 5; i++)
                await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddHours(-i));

            var result = await _service.GetRecentActivityAsync(_userId, 1, 500);

            result.PageSize.Should().Be(100);
        }

        [Fact]
        public async Task GetRecentActivityAsync_PageNegative_ClampsToOne()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow, "Step");

            var result = await _service.GetRecentActivityAsync(_userId, -5, 20);

            result.Page.Should().Be(1);
            result.Entries.Should().HaveCount(1);
        }

        // ── Data isolation ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetRecentActivityAsync_OtherUsersSteps_NotIncluded()
        {
            var otherUserId = Guid.NewGuid();
            var otherGoal = new Goal(Guid.NewGuid(), otherUserId, "Other Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(otherGoal);
            var step = new Step(Guid.NewGuid(), otherGoal.Id, "Other Step");
            step.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.AddAsync(step);

            var result = await _service.GetRecentActivityAsync(_userId, 1, 20);

            result.TotalCount.Should().Be(0);
            result.Entries.Should().BeEmpty();
        }

        // ── Mixed scenario ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetRecentActivityAsync_MixedCompletedAndPending_OnlyCompletedReturned()
        {
            var goal = await AddGoalAsync();
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow.AddHours(-1), "Done");
            await AddPendingStepAsync(goal.Id, "Todo");
            await AddCompletedStepAsync(goal.Id, DateTime.UtcNow, "Also Done");

            var result = await _service.GetRecentActivityAsync(_userId, 1, 20);

            result.TotalCount.Should().Be(2);
            result.Entries.All(e => e.StepTitle != "Todo").Should().BeTrue();
        }

        // ── GoalTitle is included ──────────────────────────────────────────────────

        [Fact]
        public async Task GetRecentActivityAsync_MultipleGoals_EntryContainsCorrectGoalTitle()
        {
            var goal1 = await AddGoalAsync("Goal Alpha");
            var goal2 = await AddGoalAsync("Goal Beta");

            await AddCompletedStepAsync(goal1.Id, DateTime.UtcNow.AddHours(-2), "Step 1");
            await AddCompletedStepAsync(goal2.Id, DateTime.UtcNow.AddHours(-1), "Step 2");

            var result = await _service.GetRecentActivityAsync(_userId, 1, 20);

            result.Entries[0].GoalTitle.Should().Be("Goal Beta");
            result.Entries[1].GoalTitle.Should().Be("Goal Alpha");
        }
    }
}
