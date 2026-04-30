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
    public class StepPriorityTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;
        private readonly Guid _userId = Guid.NewGuid();

        public StepPriorityTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_StepPriority_" + Guid.NewGuid())
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

        // ── Domain: StepPriority enum ────────────────────────────────────────────

        [Fact]
        public void StepPriority_HasExpectedValues()
        {
            ((int)StepPriority.None).Should().Be(0);
            ((int)StepPriority.Low).Should().Be(1);
            ((int)StepPriority.Medium).Should().Be(2);
            ((int)StepPriority.High).Should().Be(3);
        }

        // ── Domain entity: Priority property ────────────────────────────────────

        [Fact]
        public void Step_Constructor_DefaultPriority_IsNone()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");

            step.Priority.Should().Be(StepPriority.None);
        }

        [Theory]
        [InlineData(StepPriority.None)]
        [InlineData(StepPriority.Low)]
        [InlineData(StepPriority.Medium)]
        [InlineData(StepPriority.High)]
        public void Step_Constructor_SetsPriorityCorrectly(StepPriority priority)
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", priority: priority);

            step.Priority.Should().Be(priority);
        }

        [Fact]
        public void Step_UpdatePriority_ChangesValue()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", priority: StepPriority.Low);

            step.UpdatePriority(StepPriority.High);

            step.Priority.Should().Be(StepPriority.High);
        }

        // ── Application: CreateAsync with priority ───────────────────────────────

        [Fact]
        public async Task CreateAsync_WithHighPriority_ReturnsPriorityHigh()
        {
            var goal = await CreateGoalAsync();

            var result = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", Priority: StepPriority.High), _userId);

            result.Priority.Should().Be(StepPriority.High);
        }

        [Fact]
        public async Task CreateAsync_WithDefaultPriority_ReturnsPriorityNone()
        {
            var goal = await CreateGoalAsync();

            var result = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), _userId);

            result.Priority.Should().Be(StepPriority.None);
        }

        // ── Application: UpdateAsync with priority ───────────────────────────────

        [Fact]
        public async Task UpdateAsync_WithPriority_UpdatesPriority()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", Priority: StepPriority.Low), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Priority: StepPriority.High), _userId);

            updated.Priority.Should().Be(StepPriority.High);
        }

        [Fact]
        public async Task UpdateAsync_WithNullPriority_KeepsExistingPriority()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", Priority: StepPriority.Medium), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Notes: "note"), _userId);

            updated.Priority.Should().Be(StepPriority.Medium);
        }

        // ── Application: FilterByPriority ────────────────────────────────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_FilterByPriority_ReturnsOnlyMatchingSteps()
        {
            var goal = await CreateGoalAsync();
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Low Step", Priority: StepPriority.Low), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("High Step 1", Priority: StepPriority.High), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("High Step 2", Priority: StepPriority.High), _userId);

            var filter = new StepFilterRequest(priority: StepPriority.High);
            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, _userId, filter);

            result.Items.Should().HaveCount(2);
            result.Items.Should().OnlyContain(s => s.Priority == StepPriority.High);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_FilterByNonePriority_ReturnsOnlyNonePrioritySteps()
        {
            var goal = await CreateGoalAsync();
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("No Priority"), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("High Step", Priority: StepPriority.High), _userId);

            var filter = new StepFilterRequest(priority: StepPriority.None);
            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, _userId, filter);

            result.Items.Should().HaveCount(1);
            result.Items.First().Priority.Should().Be(StepPriority.None);
        }

        // ── Application: SortByPriority ──────────────────────────────────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_SortByPriorityAsc_ReturnsLowestFirst()
        {
            var goal = await CreateGoalAsync();
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("High", Priority: StepPriority.High), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Low", Priority: StepPriority.Low), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Medium", Priority: StepPriority.Medium), _userId);

            var filter = new StepFilterRequest(sortBy: "priority", sortOrder: "asc");
            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, _userId, filter);

            var priorities = result.Items.Select(s => s.Priority).ToList();
            priorities.Should().BeInAscendingOrder(p => (int)p);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SortByPriorityDesc_ReturnsHighestFirst()
        {
            var goal = await CreateGoalAsync();
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Low", Priority: StepPriority.Low), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Medium", Priority: StepPriority.Medium), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("High", Priority: StepPriority.High), _userId);

            var filter = new StepFilterRequest(sortBy: "priority", sortOrder: "desc");
            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, _userId, filter);

            var priorities = result.Items.Select(s => s.Priority).ToList();
            priorities.Should().BeInDescendingOrder(p => (int)p);
        }

        // ── Application: Combined filter (isCompleted + priority) ───────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_FilterByIsCompletedAndPriority_ReturnsCorrectSteps()
        {
            var goal = await CreateGoalAsync();
            var highStep = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("High Pending", Priority: StepPriority.High), _userId);
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Low Pending", Priority: StepPriority.Low), _userId);
            await _stepService.MarkCompletedAsync(goal.Id, highStep.Id, _userId);

            var filter = new StepFilterRequest(isCompleted: true, priority: StepPriority.High);
            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, _userId, filter);

            result.Items.Should().HaveCount(1);
            result.Items.First().Title.Should().Be("High Pending");
            result.Items.First().Priority.Should().Be(StepPriority.High);
            result.Items.First().IsCompleted.Should().BeTrue();
        }

        // ── Application: ListByGoalAsync includes Priority ───────────────────────

        [Fact]
        public async Task ListByGoalAsync_IncludesPriority()
        {
            var goal = await CreateGoalAsync();
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", Priority: StepPriority.Medium), _userId);

            var steps = await _stepService.ListByGoalAsync(goal.Id, _userId);

            steps.Should().HaveCount(1);
            steps[0].Priority.Should().Be(StepPriority.Medium);
        }
    }
}
