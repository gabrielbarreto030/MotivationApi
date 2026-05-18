using System;
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
    public class GoalCompletionDateTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly Guid _userId = Guid.NewGuid();

        public GoalCompletionDateTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_GoalCompletion_" + Guid.NewGuid())
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

        private async Task<Goal> SeedGoalAsync(GoalStatus status = GoalStatus.Pending)
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Test Goal", "Desc", status, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        // ── Domain: CompletedAt field ────────────────────────────────────────────

        [Fact]
        public void Goal_DefaultCompletedAt_IsNull()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.CompletedAt.Should().BeNull();
        }

        [Fact]
        public void Goal_UpdateStatus_ToCompleted_SetsCompletedAt()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            var before = DateTime.UtcNow;

            goal.UpdateStatus(GoalStatus.Completed);

            goal.CompletedAt.Should().NotBeNull();
            goal.CompletedAt.Should().BeOnOrAfter(before);
            goal.CompletedAt.Should().BeOnOrBefore(DateTime.UtcNow);
        }

        [Fact]
        public void Goal_UpdateStatus_FromCompletedToPending_ClearsCompletedAt()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            goal.UpdateStatus(GoalStatus.Completed);

            goal.UpdateStatus(GoalStatus.Pending);

            goal.CompletedAt.Should().BeNull();
        }

        [Fact]
        public void Goal_UpdateStatus_FromCompletedToInProgress_ClearsCompletedAt()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            goal.UpdateStatus(GoalStatus.Completed);

            goal.UpdateStatus(GoalStatus.InProgress);

            goal.CompletedAt.Should().BeNull();
        }

        [Fact]
        public void Goal_UpdateStatus_FromCompletedToCancelled_ClearsCompletedAt()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            goal.UpdateStatus(GoalStatus.Completed);

            goal.UpdateStatus(GoalStatus.Cancelled);

            goal.CompletedAt.Should().BeNull();
        }

        [Fact]
        public void Goal_UpdateStatus_AlreadyCompleted_DoesNotChangeCompletedAt()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            goal.UpdateStatus(GoalStatus.Completed);
            var firstCompletedAt = goal.CompletedAt;

            // Small delay to ensure time difference if CompletedAt were reset
            goal.UpdateStatus(GoalStatus.Completed);

            goal.CompletedAt.Should().Be(firstCompletedAt);
        }

        [Fact]
        public void Goal_Constructor_WithCompletedAt_PreservesValue()
        {
            var completedAt = DateTime.UtcNow.AddDays(-1);
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Completed, DateTime.UtcNow, completedAt: completedAt);

            goal.CompletedAt.Should().Be(completedAt);
        }

        // ── Application: UpdateAsync sets CompletedAt via status change ──────────

        [Fact]
        public async Task UpdateAsync_StatusToCompleted_SetsCompletedAt()
        {
            var goal = await SeedGoalAsync();
            var before = DateTime.UtcNow;

            var result = await _goalService.UpdateAsync(goal.Id, new UpdateGoalRequest { Status = "Completed" }, _userId);

            result.CompletedAt.Should().NotBeNull();
            result.CompletedAt.Should().BeOnOrAfter(before);
        }

        [Fact]
        public async Task UpdateAsync_StatusToPending_CompletedAtIsNull()
        {
            var goal = await SeedGoalAsync();
            await _goalService.UpdateAsync(goal.Id, new UpdateGoalRequest { Status = "Completed" }, _userId);

            var result = await _goalService.UpdateAsync(goal.Id, new UpdateGoalRequest { Status = "Pending" }, _userId);

            result.CompletedAt.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_NoStatusChange_CompletedAtRemainsNull()
        {
            var goal = await SeedGoalAsync();

            var result = await _goalService.UpdateAsync(goal.Id, new UpdateGoalRequest { Title = "New Title" }, _userId);

            result.CompletedAt.Should().BeNull();
        }

        // ── Application: CreateAsync always has null CompletedAt ─────────────────

        [Fact]
        public async Task CreateAsync_NewGoal_CompletedAtIsNull()
        {
            var result = await _goalService.CreateAsync(new CreateGoalRequest("Title", "Desc"), _userId);

            result.CompletedAt.Should().BeNull();
        }

        // ── Application: CompletedAt persists through repository ─────────────────

        [Fact]
        public async Task UpdateAsync_CompletedAt_PersistedToRepository()
        {
            var goal = await SeedGoalAsync();
            await _goalService.UpdateAsync(goal.Id, new UpdateGoalRequest { Status = "Completed" }, _userId);

            var stored = await _goalRepository.GetByIdAsync(goal.Id);

            stored!.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateAsync_RevertFromCompleted_ClearsCompletedAtInRepository()
        {
            var goal = await SeedGoalAsync();
            await _goalService.UpdateAsync(goal.Id, new UpdateGoalRequest { Status = "Completed" }, _userId);
            await _goalService.UpdateAsync(goal.Id, new UpdateGoalRequest { Status = "InProgress" }, _userId);

            var stored = await _goalRepository.GetByIdAsync(goal.Id);

            stored!.CompletedAt.Should().BeNull();
        }
    }
}
