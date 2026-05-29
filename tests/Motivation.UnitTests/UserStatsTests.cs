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
    public class UserStatsTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly UserStatsService _userStatsService;
        private readonly Guid _userId = Guid.NewGuid();

        public UserStatsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_UserStats_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _motivationRepository = new MotivationRepository(_context, _cache);
            _userStatsService = new UserStatsService(
                _goalRepository,
                _stepRepository,
                _motivationRepository,
                NullLogger<UserStatsService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        [Fact]
        public async Task GetStatsAsync_NoData_ReturnsAllZeros()
        {
            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.TotalGoals.Should().Be(0);
            stats.PinnedGoals.Should().Be(0);
            stats.ArchivedGoals.Should().Be(0);
            stats.OverdueGoals.Should().Be(0);
            stats.GoalsPending.Should().Be(0);
            stats.GoalsInProgress.Should().Be(0);
            stats.GoalsCompleted.Should().Be(0);
            stats.GoalsCancelled.Should().Be(0);
            stats.TotalSteps.Should().Be(0);
            stats.CompletedSteps.Should().Be(0);
            stats.PendingSteps.Should().Be(0);
            stats.OverdueSteps.Should().Be(0);
            stats.TotalMotivations.Should().Be(0);
        }

        [Fact]
        public async Task GetStatsAsync_CountsGoalsByStatus()
        {
            await AddGoal(GoalStatus.Pending);
            await AddGoal(GoalStatus.InProgress);
            await AddGoal(GoalStatus.InProgress);
            await AddGoal(GoalStatus.Completed);
            await AddGoal(GoalStatus.Cancelled);

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.TotalGoals.Should().Be(5);
            stats.GoalsPending.Should().Be(1);
            stats.GoalsInProgress.Should().Be(2);
            stats.GoalsCompleted.Should().Be(1);
            stats.GoalsCancelled.Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_CountsPinnedGoals()
        {
            var goalId1 = await AddGoal(GoalStatus.Pending);
            var goalId2 = await AddGoal(GoalStatus.Pending);
            var goal1 = await _goalRepository.GetByIdAsync(goalId1);
            goal1!.Pin();
            await _goalRepository.UpdateAsync(goal1);

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.PinnedGoals.Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_CountsArchivedGoals()
        {
            var goalId1 = await AddGoal(GoalStatus.Pending);
            await AddGoal(GoalStatus.Pending);
            var goal1 = await _goalRepository.GetByIdAsync(goalId1);
            goal1!.Archive();
            await _goalRepository.UpdateAsync(goal1);

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.ArchivedGoals.Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_CountsOverdueGoals()
        {
            var pastDeadline = DateTime.UtcNow.AddDays(-1);
            var futureDeadline = DateTime.UtcNow.AddDays(1);

            var goalId1 = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(goalId1, _userId, "Overdue", "Desc", GoalStatus.InProgress, DateTime.UtcNow, deadline: pastDeadline));
            var goalId2 = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(goalId2, _userId, "Not Overdue", "Desc", GoalStatus.InProgress, DateTime.UtcNow, deadline: futureDeadline));

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.OverdueGoals.Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_CompletedGoal_NotCountedAsOverdue()
        {
            var pastDeadline = DateTime.UtcNow.AddDays(-1);
            var goalId = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(goalId, _userId, "Completed", "Desc", GoalStatus.Completed, DateTime.UtcNow, deadline: pastDeadline));

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.OverdueGoals.Should().Be(0);
        }

        [Fact]
        public async Task GetStatsAsync_CountsStepsCorrectly()
        {
            var goalId = await AddGoal(GoalStatus.InProgress);

            var step1 = new Step(Guid.NewGuid(), goalId, "Step 1", order: 1);
            var step2 = new Step(Guid.NewGuid(), goalId, "Step 2", order: 2);
            var step3 = new Step(Guid.NewGuid(), goalId, "Step 3", order: 3);
            step3.MarkCompleted(DateTime.UtcNow);

            await _stepRepository.AddAsync(step1);
            await _stepRepository.AddAsync(step2);
            await _stepRepository.AddAsync(step3);

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.TotalSteps.Should().Be(3);
            stats.CompletedSteps.Should().Be(1);
            stats.PendingSteps.Should().Be(2);
        }

        [Fact]
        public async Task GetStatsAsync_CountsOverdueSteps()
        {
            var goalId = await AddGoal(GoalStatus.InProgress);
            var pastDue = DateTime.UtcNow.AddDays(-1);
            var futureDue = DateTime.UtcNow.AddDays(1);

            var overdueStep = new Step(Guid.NewGuid(), goalId, "Overdue Step", dueDate: pastDue, order: 1);
            var okStep = new Step(Guid.NewGuid(), goalId, "OK Step", dueDate: futureDue, order: 2);
            var completedOverdue = new Step(Guid.NewGuid(), goalId, "Completed Overdue", dueDate: pastDue, order: 3);
            completedOverdue.MarkCompleted(DateTime.UtcNow);

            await _stepRepository.AddAsync(overdueStep);
            await _stepRepository.AddAsync(okStep);
            await _stepRepository.AddAsync(completedOverdue);

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.OverdueSteps.Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_CountsMotivations()
        {
            var goalId1 = await AddGoal(GoalStatus.Pending);
            var goalId2 = await AddGoal(GoalStatus.Pending);

            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), goalId1, "Phrase 1"));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), goalId1, "Phrase 2"));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), goalId2, "Phrase 3"));

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.TotalMotivations.Should().Be(3);
        }

        [Fact]
        public async Task GetStatsAsync_DoesNotIncludeOtherUsersData()
        {
            var otherUserId = Guid.NewGuid();
            var otherGoalId = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(otherGoalId, otherUserId, "Other Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow));

            await AddGoal(GoalStatus.Pending);

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.TotalGoals.Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_StepsAcrossMultipleGoals_AggregatesCorrectly()
        {
            var goalId1 = await AddGoal(GoalStatus.InProgress);
            var goalId2 = await AddGoal(GoalStatus.InProgress);

            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goalId1, "Step A", order: 1));
            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goalId1, "Step B", order: 2));
            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goalId2, "Step C", order: 1));

            var stats = await _userStatsService.GetStatsAsync(_userId);

            stats.TotalSteps.Should().Be(3);
        }

        private async Task<Guid> AddGoal(GoalStatus status)
        {
            var id = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(id, _userId, "Goal " + id, "Desc", status, DateTime.UtcNow));
            return id;
        }
    }
}
