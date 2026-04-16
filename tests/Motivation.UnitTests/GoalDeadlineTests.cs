using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Motivation.Application.DTOs;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class GoalDeadlineTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;

        public GoalDeadlineTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Deadline_" + Guid.NewGuid())
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

        // ── Domain entity: IsOverdue ─────────────────────────────────────────────

        [Fact]
        public void IsOverdue_NoDeadline_ReturnsFalse()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "T", "D", GoalStatus.Pending, DateTime.UtcNow);

            goal.IsOverdue(DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void IsOverdue_FutureDeadline_ReturnsFalse()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "T", "D", GoalStatus.Pending, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(7));

            goal.IsOverdue(DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void IsOverdue_PastDeadlinePending_ReturnsTrue()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "T", "D", GoalStatus.Pending, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(-1));

            goal.IsOverdue(DateTime.UtcNow).Should().BeTrue();
        }

        [Fact]
        public void IsOverdue_PastDeadlineInProgress_ReturnsTrue()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "T", "D", GoalStatus.InProgress, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(-1));

            goal.IsOverdue(DateTime.UtcNow).Should().BeTrue();
        }

        [Fact]
        public void IsOverdue_PastDeadlineCompleted_ReturnsFalse()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "T", "D", GoalStatus.Completed, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(-1));

            goal.IsOverdue(DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void IsOverdue_PastDeadlineCancelled_ReturnsFalse()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "T", "D", GoalStatus.Cancelled, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(-1));

            goal.IsOverdue(DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void UpdateDeadline_SetsDeadline()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "T", "D", GoalStatus.Pending, DateTime.UtcNow);
            var newDeadline = DateTime.UtcNow.AddDays(5);

            goal.UpdateDeadline(newDeadline);

            goal.Deadline.Should().Be(newDeadline);
        }

        [Fact]
        public void UpdateDeadline_WithNull_ClearsDeadline()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "T", "D", GoalStatus.Pending, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(3));

            goal.UpdateDeadline(null);

            goal.Deadline.Should().BeNull();
        }

        // ── CreateAsync with Deadline ────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WithDeadline_PersistsDeadline()
        {
            var userId = Guid.NewGuid();
            var deadline = DateTime.UtcNow.AddDays(10);
            var request = new CreateGoalRequest("Goal with deadline", "Desc", deadline);

            var result = await _goalService.CreateAsync(request, userId);

            result.Deadline.Should().BeCloseTo(deadline, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task CreateAsync_WithoutDeadline_DeadlineIsNull()
        {
            var userId = Guid.NewGuid();
            var request = new CreateGoalRequest("Goal no deadline", "Desc");

            var result = await _goalService.CreateAsync(request, userId);

            result.Deadline.Should().BeNull();
        }

        [Fact]
        public async Task CreateAsync_WithFutureDeadline_IsOverdueFalse()
        {
            var userId = Guid.NewGuid();
            var request = new CreateGoalRequest("Future goal", "Desc", DateTime.UtcNow.AddDays(5));

            var result = await _goalService.CreateAsync(request, userId);

            result.IsOverdue.Should().BeFalse();
        }

        // ── UpdateAsync with Deadline ────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_SetDeadline_PersistsDeadline()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var deadline = DateTime.UtcNow.AddDays(7);
            var request = new UpdateGoalRequest(null, null, null, deadline);

            var result = await _goalService.UpdateAsync(goal.Id, request, userId);

            result.Deadline.Should().BeCloseTo(deadline, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task UpdateAsync_ClearDeadline_RemovesDeadline()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(5));
            await _goalRepository.AddAsync(goal);

            var request = new UpdateGoalRequest(null, null, null, null, ClearDeadline: true);

            var result = await _goalService.UpdateAsync(goal.Id, request, userId);

            result.Deadline.Should().BeNull();
        }

        // ── GetOverdueAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetOverdueAsync_NoGoals_ReturnsEmpty()
        {
            var userId = Guid.NewGuid();

            var result = await _goalService.GetOverdueAsync(userId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOverdueAsync_NoOverdueGoals_ReturnsEmpty()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(5));
            await _goalRepository.AddAsync(goal);

            var result = await _goalService.GetOverdueAsync(userId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOverdueAsync_GoalWithNullDeadline_NotIncluded()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var result = await _goalService.GetOverdueAsync(userId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOverdueAsync_OverdueGoal_IsReturned()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Overdue", "D", GoalStatus.Pending, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(-1));
            await _goalRepository.AddAsync(goal);

            var result = await _goalService.GetOverdueAsync(userId);

            result.Should().HaveCount(1);
            result[0].Title.Should().Be("Overdue");
            result[0].IsOverdue.Should().BeTrue();
        }

        [Fact]
        public async Task GetOverdueAsync_CompletedPastDeadline_NotIncluded()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Done", "D", GoalStatus.Completed, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(-1));
            await _goalRepository.AddAsync(goal);

            var result = await _goalService.GetOverdueAsync(userId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOverdueAsync_CancelledPastDeadline_NotIncluded()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Cancelled", "D", GoalStatus.Cancelled, DateTime.UtcNow,
                deadline: DateTime.UtcNow.AddDays(-1));
            await _goalRepository.AddAsync(goal);

            var result = await _goalService.GetOverdueAsync(userId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOverdueAsync_MixedGoals_ReturnsOnlyOverdue()
        {
            var userId = Guid.NewGuid();

            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "OK future", "D", GoalStatus.Pending, DateTime.UtcNow, DateTime.UtcNow.AddDays(5)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "OK no deadline", "D", GoalStatus.Pending, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Overdue1", "D", GoalStatus.Pending, DateTime.UtcNow, DateTime.UtcNow.AddDays(-2)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Overdue2", "D", GoalStatus.InProgress, DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Done past", "D", GoalStatus.Completed, DateTime.UtcNow, DateTime.UtcNow.AddDays(-3)));

            var result = await _goalService.GetOverdueAsync(userId);

            result.Should().HaveCount(2);
            result.Select(r => r.Title).Should().Contain(new[] { "Overdue1", "Overdue2" });
            result.All(r => r.IsOverdue).Should().BeTrue();
        }

        [Fact]
        public async Task GetOverdueAsync_IsolatedByUser_OtherUsersGoalsIgnored()
        {
            var userId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), otherId, "Other overdue", "D", GoalStatus.Pending, DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "My future", "D", GoalStatus.Pending, DateTime.UtcNow, DateTime.UtcNow.AddDays(5)));

            var result = await _goalService.GetOverdueAsync(userId);

            result.Should().BeEmpty();
        }
    }
}
