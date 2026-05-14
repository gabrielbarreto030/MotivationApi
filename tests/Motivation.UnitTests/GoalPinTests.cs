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
    public class GoalPinTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly Guid _userId = Guid.NewGuid();

        public GoalPinTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_GoalPin_" + Guid.NewGuid())
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

        private async Task<Goal> SeedGoalAsync(string title = "Goal")
        {
            var goal = new Goal(Guid.NewGuid(), _userId, title, "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        // ── Domain: Pin/Unpin ────────────────────────────────────────────────────

        [Fact]
        public void Goal_DefaultIsPinned_IsFalse()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.IsPinned.Should().BeFalse();
        }

        [Fact]
        public void Goal_Pin_SetsIsPinnedTrue()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.Pin();

            goal.IsPinned.Should().BeTrue();
        }

        [Fact]
        public void Goal_Unpin_SetsIsPinnedFalse()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            goal.Pin();

            goal.Unpin();

            goal.IsPinned.Should().BeFalse();
        }

        // ── Application: PinAsync ────────────────────────────────────────────────

        [Fact]
        public async Task PinAsync_ValidGoal_ReturnsIsPinnedTrue()
        {
            var goal = await SeedGoalAsync();

            var result = await _goalService.PinAsync(goal.Id, _userId);

            result.IsPinned.Should().BeTrue();
        }

        [Fact]
        public async Task PinAsync_PersistsToRepository()
        {
            var goal = await SeedGoalAsync();

            await _goalService.PinAsync(goal.Id, _userId);

            var stored = await _goalRepository.GetByIdAsync(goal.Id);
            stored!.IsPinned.Should().BeTrue();
        }

        [Fact]
        public async Task PinAsync_GoalNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _goalService.PinAsync(Guid.NewGuid(), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task PinAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            var goal = await SeedGoalAsync();

            Func<Task> act = async () => await _goalService.PinAsync(goal.Id, Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task PinAsync_AlreadyPinned_ThrowsInvalidOperationException()
        {
            var goal = await SeedGoalAsync();
            await _goalService.PinAsync(goal.Id, _userId);

            Func<Task> act = async () => await _goalService.PinAsync(goal.Id, _userId);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already pinned*");
        }

        [Fact]
        public async Task PinAsync_ExceedsLimit_ThrowsInvalidOperationException()
        {
            var g1 = await SeedGoalAsync("Goal 1");
            var g2 = await SeedGoalAsync("Goal 2");
            var g3 = await SeedGoalAsync("Goal 3");
            var g4 = await SeedGoalAsync("Goal 4");

            await _goalService.PinAsync(g1.Id, _userId);
            await _goalService.PinAsync(g2.Id, _userId);
            await _goalService.PinAsync(g3.Id, _userId);

            Func<Task> act = async () => await _goalService.PinAsync(g4.Id, _userId);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*at most 3*");
        }

        // ── Application: UnpinAsync ──────────────────────────────────────────────

        [Fact]
        public async Task UnpinAsync_PinnedGoal_ReturnsIsPinnedFalse()
        {
            var goal = await SeedGoalAsync();
            await _goalService.PinAsync(goal.Id, _userId);

            var result = await _goalService.UnpinAsync(goal.Id, _userId);

            result.IsPinned.Should().BeFalse();
        }

        [Fact]
        public async Task UnpinAsync_GoalNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _goalService.UnpinAsync(Guid.NewGuid(), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task UnpinAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            var goal = await SeedGoalAsync();
            await _goalService.PinAsync(goal.Id, _userId);

            Func<Task> act = async () => await _goalService.UnpinAsync(goal.Id, Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task UnpinAsync_NotPinned_ThrowsInvalidOperationException()
        {
            var goal = await SeedGoalAsync();

            Func<Task> act = async () => await _goalService.UnpinAsync(goal.Id, _userId);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not pinned*");
        }

        // ── Application: GetPinnedAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetPinnedAsync_NoPinnedGoals_ReturnsEmpty()
        {
            await SeedGoalAsync("Unpinned");

            var result = await _goalService.GetPinnedAsync(_userId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetPinnedAsync_ReturnsOnlyPinnedGoals()
        {
            var unpinned = await SeedGoalAsync("Unpinned");
            var pinned = await SeedGoalAsync("Pinned");
            await _goalService.PinAsync(pinned.Id, _userId);

            var result = await _goalService.GetPinnedAsync(_userId);

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(pinned.Id);
            result[0].IsPinned.Should().BeTrue();
        }

        [Fact]
        public async Task GetPinnedAsync_UpToThreePinnedGoals_ReturnsAll()
        {
            var g1 = await SeedGoalAsync("G1");
            var g2 = await SeedGoalAsync("G2");
            var g3 = await SeedGoalAsync("G3");

            await _goalService.PinAsync(g1.Id, _userId);
            await _goalService.PinAsync(g2.Id, _userId);
            await _goalService.PinAsync(g3.Id, _userId);

            var result = await _goalService.GetPinnedAsync(_userId);

            result.Should().HaveCount(3);
            result.Should().OnlyContain(g => g.IsPinned);
        }

        // ── Application: CreateGoalResponse includes IsPinned ────────────────────

        [Fact]
        public async Task CreateAsync_NewGoal_IsPinnedFalseInResponse()
        {
            var result = await _goalService.CreateAsync(new CreateGoalRequest("Title", "Desc"), _userId);

            result.IsPinned.Should().BeFalse();
        }
    }
}
