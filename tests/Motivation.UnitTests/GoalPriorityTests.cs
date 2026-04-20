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
    public class GoalPriorityTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly Guid _userId = Guid.NewGuid();

        public GoalPriorityTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Priority_" + Guid.NewGuid())
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

        // ── Domain: GoalPriority enum ────────────────────────────────────────────

        [Fact]
        public void GoalPriority_HasExpectedValues()
        {
            ((int)GoalPriority.None).Should().Be(0);
            ((int)GoalPriority.Low).Should().Be(1);
            ((int)GoalPriority.Medium).Should().Be(2);
            ((int)GoalPriority.High).Should().Be(3);
        }

        // ── Domain entity: Priority property ────────────────────────────────────

        [Fact]
        public void Goal_Constructor_DefaultPriority_IsNone()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.Priority.Should().Be(GoalPriority.None);
        }

        [Theory]
        [InlineData(GoalPriority.None)]
        [InlineData(GoalPriority.Low)]
        [InlineData(GoalPriority.Medium)]
        [InlineData(GoalPriority.High)]
        public void Goal_Constructor_SetsPriorityCorrectly(GoalPriority priority)
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow, null, priority);

            goal.Priority.Should().Be(priority);
        }

        [Fact]
        public void Goal_UpdatePriority_ChangesValue()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow, null, GoalPriority.Low);

            goal.UpdatePriority(GoalPriority.High);

            goal.Priority.Should().Be(GoalPriority.High);
        }

        [Fact]
        public void Goal_Update_WithPriority_ChangesPriority()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.Update(null, null, null, null, false, GoalPriority.Medium);

            goal.Priority.Should().Be(GoalPriority.Medium);
        }

        [Fact]
        public void Goal_Update_WithNullPriority_KeepsExistingPriority()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow, null, GoalPriority.High);

            goal.Update(null, null, null, null, false, null);

            goal.Priority.Should().Be(GoalPriority.High);
        }

        // ── Application: CreateAsync with priority ───────────────────────────────

        [Fact]
        public async Task CreateAsync_WithHighPriority_ReturnsPriorityHigh()
        {
            var request = new CreateGoalRequest("High Priority Goal", "Description", null, GoalPriority.High);

            var result = await _goalService.CreateAsync(request, _userId);

            result.Priority.Should().Be(GoalPriority.High);
        }

        [Fact]
        public async Task CreateAsync_WithDefaultPriority_ReturnsPriorityNone()
        {
            var request = new CreateGoalRequest("Default Goal", "Description");

            var result = await _goalService.CreateAsync(request, _userId);

            result.Priority.Should().Be(GoalPriority.None);
        }

        // ── Application: UpdateAsync with priority ───────────────────────────────

        [Fact]
        public async Task UpdateAsync_WithPriority_UpdatesPriority()
        {
            var created = await _goalService.CreateAsync(
                new CreateGoalRequest("Goal", "Desc", null, GoalPriority.Low), _userId);

            var updateRequest = new UpdateGoalRequest(null, null, null, null, false, GoalPriority.High);
            var updated = await _goalService.UpdateAsync(created.Id, updateRequest, _userId);

            updated.Priority.Should().Be(GoalPriority.High);
        }

        [Fact]
        public async Task UpdateAsync_WithNullPriority_KeepsExistingPriority()
        {
            var created = await _goalService.CreateAsync(
                new CreateGoalRequest("Goal", "Desc", null, GoalPriority.Medium), _userId);

            var updateRequest = new UpdateGoalRequest("New Title", null, null);
            var updated = await _goalService.UpdateAsync(created.Id, updateRequest, _userId);

            updated.Priority.Should().Be(GoalPriority.Medium);
        }

        // ── Application: FilterByPriority ────────────────────────────────────────

        [Fact]
        public async Task ListByUserFilteredAsync_FilterByPriority_ReturnsOnlyMatchingGoals()
        {
            await _goalService.CreateAsync(new CreateGoalRequest("Low Goal", "Desc", null, GoalPriority.Low), _userId);
            await _goalService.CreateAsync(new CreateGoalRequest("High Goal", "Desc", null, GoalPriority.High), _userId);
            await _goalService.CreateAsync(new CreateGoalRequest("High Goal 2", "Desc", null, GoalPriority.High), _userId);

            var filter = new GoalFilterRequest(priority: GoalPriority.High);
            var result = await _goalService.ListByUserFilteredAsync(_userId, filter);

            result.Items.Should().HaveCount(2);
            result.Items.Should().OnlyContain(g => g.Priority == GoalPriority.High);
        }

        [Fact]
        public async Task ListByUserFilteredAsync_FilterByNonePriority_ReturnsOnlyNonePriorityGoals()
        {
            await _goalService.CreateAsync(new CreateGoalRequest("No Priority Goal", "Desc"), _userId);
            await _goalService.CreateAsync(new CreateGoalRequest("High Goal", "Desc", null, GoalPriority.High), _userId);

            var filter = new GoalFilterRequest(priority: GoalPriority.None);
            var result = await _goalService.ListByUserFilteredAsync(_userId, filter);

            result.Items.Should().HaveCount(1);
            result.Items.First().Priority.Should().Be(GoalPriority.None);
        }

        // ── Application: SortByPriority ───────────────────────────────────────────

        [Fact]
        public async Task ListByUserFilteredAsync_SortByPriorityAsc_ReturnsLowestFirst()
        {
            await _goalService.CreateAsync(new CreateGoalRequest("High", "Desc", null, GoalPriority.High), _userId);
            await _goalService.CreateAsync(new CreateGoalRequest("Low", "Desc", null, GoalPriority.Low), _userId);
            await _goalService.CreateAsync(new CreateGoalRequest("Medium", "Desc", null, GoalPriority.Medium), _userId);

            var filter = new GoalFilterRequest(sortBy: "priority", sortOrder: "asc");
            var result = await _goalService.ListByUserFilteredAsync(_userId, filter);

            var priorities = result.Items.Select(g => g.Priority).ToList();
            priorities.Should().BeInAscendingOrder(p => (int)p);
        }

        [Fact]
        public async Task ListByUserFilteredAsync_SortByPriorityDesc_ReturnsHighestFirst()
        {
            await _goalService.CreateAsync(new CreateGoalRequest("Low", "Desc", null, GoalPriority.Low), _userId);
            await _goalService.CreateAsync(new CreateGoalRequest("Medium", "Desc", null, GoalPriority.Medium), _userId);
            await _goalService.CreateAsync(new CreateGoalRequest("High", "Desc", null, GoalPriority.High), _userId);

            var filter = new GoalFilterRequest(sortBy: "priority", sortOrder: "desc");
            var result = await _goalService.ListByUserFilteredAsync(_userId, filter);

            var priorities = result.Items.Select(g => g.Priority).ToList();
            priorities.Should().BeInDescendingOrder(p => (int)p);
        }

        // ── Application: Combined filter (status + priority) ────────────────────

        [Fact]
        public async Task ListByUserFilteredAsync_FilterByStatusAndPriority_ReturnsCorrectGoals()
        {
            await _goalService.CreateAsync(new CreateGoalRequest("Pending High", "Desc", null, GoalPriority.High), _userId);
            await _goalService.CreateAsync(new CreateGoalRequest("Pending Low", "Desc", null, GoalPriority.Low), _userId);

            // Update the second goal to InProgress
            var all = await _goalService.ListByUserAsync(_userId);
            var pendingHigh = all.First(g => g.Title == "Pending High");
            await _goalService.UpdateAsync(pendingHigh.Id, new UpdateGoalRequest(null, null, "InProgress", null, false, GoalPriority.High), _userId);

            var filter = new GoalFilterRequest(status: GoalStatus.InProgress, priority: GoalPriority.High);
            var result = await _goalService.ListByUserFilteredAsync(_userId, filter);

            result.Items.Should().HaveCount(1);
            result.Items.First().Title.Should().Be("Pending High");
            result.Items.First().Priority.Should().Be(GoalPriority.High);
        }
    }
}
