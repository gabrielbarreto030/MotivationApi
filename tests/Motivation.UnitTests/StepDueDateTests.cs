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
    public class StepDueDateTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;
        private readonly Guid _userId = Guid.NewGuid();

        public StepDueDateTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_StepDueDate_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _stepService = new StepService(_stepRepository, _goalRepository, NullLogger<StepService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> CreateGoalAsync()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Test Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        // ── Domain: DueDate + IsOverdue ──────────────────────────────────────────

        [Fact]
        public void Step_Constructor_SetsDueDate()
        {
            var due = DateTime.UtcNow.AddDays(3);
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", dueDate: due);

            step.DueDate.Should().Be(due);
        }

        [Fact]
        public void Step_DefaultDueDate_IsNull()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");

            step.DueDate.Should().BeNull();
        }

        [Fact]
        public void Step_IsOverdue_ReturnsFalse_WhenNoDueDate()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");

            step.IsOverdue(DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void Step_IsOverdue_ReturnsFalse_WhenDueDateInFuture()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", dueDate: DateTime.UtcNow.AddDays(1));

            step.IsOverdue(DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void Step_IsOverdue_ReturnsTrue_WhenDueDateInPastAndNotCompleted()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", dueDate: DateTime.UtcNow.AddDays(-1));

            step.IsOverdue(DateTime.UtcNow).Should().BeTrue();
        }

        [Fact]
        public void Step_IsOverdue_ReturnsFalse_WhenCompleted()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", dueDate: DateTime.UtcNow.AddDays(-1));
            step.MarkCompleted(DateTime.UtcNow);

            step.IsOverdue(DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void Step_UpdateDueDate_ChangesDueDate()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");
            var due = DateTime.UtcNow.AddDays(5);

            step.UpdateDueDate(due);

            step.DueDate.Should().Be(due);
        }

        [Fact]
        public void Step_UpdateDueDate_WithNull_ClearsDueDate()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", dueDate: DateTime.UtcNow.AddDays(3));

            step.UpdateDueDate(null);

            step.DueDate.Should().BeNull();
        }

        // ── Application: CreateAsync with DueDate ───────────────────────────────

        [Fact]
        public async Task CreateAsync_WithDueDate_SetsDueDateInResponse()
        {
            var goal = await CreateGoalAsync();
            var due = DateTime.UtcNow.AddDays(7);

            var result = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", DueDate: due), _userId);

            result.DueDate.Should().BeCloseTo(due, TimeSpan.FromSeconds(1));
            result.IsOverdue.Should().BeFalse();
        }

        [Fact]
        public async Task CreateAsync_WithoutDueDate_DueDateIsNull()
        {
            var goal = await CreateGoalAsync();

            var result = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), _userId);

            result.DueDate.Should().BeNull();
            result.IsOverdue.Should().BeFalse();
        }

        [Fact]
        public async Task CreateAsync_WithPastDueDate_IsOverdueIsTrue()
        {
            var goal = await CreateGoalAsync();
            var due = DateTime.UtcNow.AddDays(-1);

            var result = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", DueDate: due), _userId);

            result.IsOverdue.Should().BeTrue();
        }

        // ── Application: UpdateAsync with DueDate ───────────────────────────────

        [Fact]
        public async Task UpdateAsync_WithDueDate_SetsDueDate()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), _userId);
            var due = DateTime.UtcNow.AddDays(10);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(DueDate: due), _userId);

            updated.DueDate.Should().BeCloseTo(due, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task UpdateAsync_WithClearDueDate_ClearsDueDate()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", DueDate: DateTime.UtcNow.AddDays(5)), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(ClearDueDate: true), _userId);

            updated.DueDate.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_WithoutDueDateFields_PreservesExistingDueDate()
        {
            var goal = await CreateGoalAsync();
            var due = DateTime.UtcNow.AddDays(5);
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", DueDate: due), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Notes: "Note"), _userId);

            updated.DueDate.Should().BeCloseTo(due, TimeSpan.FromSeconds(1));
        }

        // ── Application: GetOverdueByGoalAsync ──────────────────────────────────

        [Fact]
        public async Task GetOverdueByGoalAsync_ReturnsOnlyOverdueSteps()
        {
            var goal = await CreateGoalAsync();
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Past", DueDate: DateTime.UtcNow.AddDays(-2)), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Future", DueDate: DateTime.UtcNow.AddDays(2)), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("NoDue"), _userId);

            var overdue = await _stepService.GetOverdueByGoalAsync(goal.Id, _userId);

            overdue.Should().HaveCount(1);
            overdue[0].Title.Should().Be("Past");
            overdue[0].IsOverdue.Should().BeTrue();
        }

        [Fact]
        public async Task GetOverdueByGoalAsync_ExcludesCompletedOverdueSteps()
        {
            var goal = await CreateGoalAsync();
            var step = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Past", DueDate: DateTime.UtcNow.AddDays(-2)), _userId);
            await _stepService.MarkCompletedAsync(goal.Id, step.Id, _userId);

            var overdue = await _stepService.GetOverdueByGoalAsync(goal.Id, _userId);

            overdue.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOverdueByGoalAsync_EmptyWhenNoOverdueSteps()
        {
            var goal = await CreateGoalAsync();
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Future", DueDate: DateTime.UtcNow.AddDays(2)), _userId);

            var overdue = await _stepService.GetOverdueByGoalAsync(goal.Id, _userId);

            overdue.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOverdueByGoalAsync_WrongUser_ThrowsUnauthorized()
        {
            var goal = await CreateGoalAsync();
            var otherUser = Guid.NewGuid();

            var act = async () => await _stepService.GetOverdueByGoalAsync(goal.Id, otherUser);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task GetOverdueByGoalAsync_GoalNotFound_ThrowsArgumentException()
        {
            var act = async () => await _stepService.GetOverdueByGoalAsync(Guid.NewGuid(), _userId);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        // ── Application: ListByGoal reflects DueDate + IsOverdue ────────────────

        [Fact]
        public async Task ListByGoalAsync_IncludesDueDateAndIsOverdue()
        {
            var goal = await CreateGoalAsync();
            var due = DateTime.UtcNow.AddDays(-1);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", DueDate: due), _userId);

            var steps = await _stepService.ListByGoalAsync(goal.Id, _userId);

            steps.Should().HaveCount(1);
            steps[0].DueDate.Should().NotBeNull();
            steps[0].IsOverdue.Should().BeTrue();
        }
    }
}
