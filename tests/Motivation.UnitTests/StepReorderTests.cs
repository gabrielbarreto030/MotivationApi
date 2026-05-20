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
    public class StepReorderTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;
        private readonly Guid _userId = Guid.NewGuid();

        public StepReorderTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_StepReorder_" + Guid.NewGuid())
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

        // ── Domain entity: Order property ────────────────────────────────────────

        [Fact]
        public void Step_Constructor_DefaultOrder_IsZero()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");

            step.Order.Should().Be(0);
        }

        [Fact]
        public void Step_Constructor_SetsOrderCorrectly()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", order: 3);

            step.Order.Should().Be(3);
        }

        [Fact]
        public void Step_UpdateOrder_ChangesValue()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", order: 1);

            step.UpdateOrder(5);

            step.Order.Should().Be(5);
        }

        [Fact]
        public void Step_UpdateOrder_WithZero_ThrowsArgumentException()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", order: 1);

            var act = () => step.UpdateOrder(0);

            act.Should().Throw<ArgumentException>().WithMessage("*Order must be greater than zero*");
        }

        [Fact]
        public void Step_UpdateOrder_WithNegative_ThrowsArgumentException()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", order: 1);

            var act = () => step.UpdateOrder(-1);

            act.Should().Throw<ArgumentException>().WithMessage("*Order must be greater than zero*");
        }

        // ── Application: auto-order on creation ─────────────────────────────────

        [Fact]
        public async Task CreateAsync_FirstStep_HasOrder1()
        {
            var goal = await CreateGoalAsync();

            var result = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step A"), _userId);

            result.Order.Should().Be(1);
        }

        [Fact]
        public async Task CreateAsync_ThreeSteps_HaveSequentialOrders()
        {
            var goal = await CreateGoalAsync();

            var s1 = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step 1"), _userId);
            var s2 = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step 2"), _userId);
            var s3 = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step 3"), _userId);

            s1.Order.Should().Be(1);
            s2.Order.Should().Be(2);
            s3.Order.Should().Be(3);
        }

        // ── Application: ReorderAsync ────────────────────────────────────────────

        [Fact]
        public async Task ReorderAsync_ValidOrder_UpdatesStepOrder()
        {
            var goal = await CreateGoalAsync();
            var step = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), _userId);

            var result = await _stepService.ReorderAsync(goal.Id, step.Id, new ReorderStepRequest(5), _userId);

            result.Order.Should().Be(5);
        }

        [Fact]
        public async Task ReorderAsync_InvalidOrder_ThrowsArgumentException()
        {
            var goal = await CreateGoalAsync();
            var step = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), _userId);

            var act = async () => await _stepService.ReorderAsync(goal.Id, step.Id, new ReorderStepRequest(0), _userId);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task ReorderAsync_StepNotFound_ThrowsArgumentException()
        {
            var goal = await CreateGoalAsync();

            var act = async () => await _stepService.ReorderAsync(goal.Id, Guid.NewGuid(), new ReorderStepRequest(2), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Step not found*");
        }

        [Fact]
        public async Task ReorderAsync_GoalBelongsToOtherUser_ThrowsUnauthorizedAccessException()
        {
            var goal = await CreateGoalAsync();
            var step = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), _userId);
            var otherUserId = Guid.NewGuid();

            var act = async () => await _stepService.ReorderAsync(goal.Id, step.Id, new ReorderStepRequest(2), otherUserId);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task ReorderAsync_StepBelongsToDifferentGoal_ThrowsArgumentException()
        {
            var goal1 = await CreateGoalAsync();
            var goal2 = await CreateGoalAsync();
            var step = await _stepService.CreateAsync(goal1.Id, new CreateStepRequest("Step"), _userId);

            var act = async () => await _stepService.ReorderAsync(goal2.Id, step.Id, new ReorderStepRequest(1), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not belong*");
        }

        // ── Application: ListByGoalAsync sorted by Order ─────────────────────────

        [Fact]
        public async Task ListByGoalAsync_ReturnsSortedByOrderAsc()
        {
            var goal = await CreateGoalAsync();
            var s1 = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step 1"), _userId);
            var s2 = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step 2"), _userId);
            var s3 = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step 3"), _userId);

            // Reorder: put s3 first, s1 last
            await _stepService.ReorderAsync(goal.Id, s3.Id, new ReorderStepRequest(1), _userId);
            await _stepService.ReorderAsync(goal.Id, s2.Id, new ReorderStepRequest(2), _userId);
            await _stepService.ReorderAsync(goal.Id, s1.Id, new ReorderStepRequest(10), _userId);

            var steps = await _stepService.ListByGoalAsync(goal.Id, _userId);

            steps.Select(s => s.Order).Should().BeInAscendingOrder();
            steps[0].Title.Should().Be("Step 3");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_DefaultSortIsOrderAsc()
        {
            var goal = await CreateGoalAsync();
            var s1 = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Alpha"), _userId);
            var s2 = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Beta"), _userId);

            await _stepService.ReorderAsync(goal.Id, s2.Id, new ReorderStepRequest(1), _userId);
            await _stepService.ReorderAsync(goal.Id, s1.Id, new ReorderStepRequest(2), _userId);

            var filter = new StepFilterRequest();
            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, _userId, filter);

            result.Items.Select(s => s.Order).Should().BeInAscendingOrder();
            result.Items[0].Title.Should().Be("Beta");
        }

        // ── Application: Order included in response ──────────────────────────────

        [Fact]
        public async Task CreateStepResponse_IncludesOrder()
        {
            var goal = await CreateGoalAsync();

            var result = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), _userId);

            result.Order.Should().BeGreaterThan(0);
        }
    }
}
