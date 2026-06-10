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
    public class StepStatsTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _goalId;

        public StepStatsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_StepStats_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _stepService = new StepService(_stepRepository, _goalRepository, NullLogger<StepService>.Instance);

            _goalId = Guid.NewGuid();
            var goal = new Goal(_goalId, _userId, "Test Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            _goalRepository.AddAsync(goal).Wait();
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<CreateStepResponse> AddStepAsync(
            string title = "Step",
            bool complete = false,
            DateTime? dueDate = null,
            StepPriority priority = StepPriority.None,
            string[]? tags = null)
        {
            var req = new CreateStepRequest(title, null, dueDate, priority, tags);
            var step = await _stepService.CreateAsync(_goalId, req, _userId);
            if (complete)
                await _stepService.MarkCompletedAsync(_goalId, step.Id, _userId);
            return step;
        }

        // ── Empty goal ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_NoSteps_ReturnsZeroStats()
        {
            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.TotalSteps.Should().Be(0);
            stats.CompletedSteps.Should().Be(0);
            stats.PendingSteps.Should().Be(0);
            stats.OverdueSteps.Should().Be(0);
            stats.CompletionPercentage.Should().Be(0.0);
            stats.PriorityBreakdown.Should().BeEmpty();
            stats.TagBreakdown.Should().BeEmpty();
        }

        // ── TotalSteps ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_ThreeSteps_TotalIsThree()
        {
            await AddStepAsync("A");
            await AddStepAsync("B");
            await AddStepAsync("C");

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.TotalSteps.Should().Be(3);
        }

        // ── CompletedSteps / PendingSteps ─────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_TwoCompletedOnePending_CountsCorrectly()
        {
            await AddStepAsync("A", complete: true);
            await AddStepAsync("B", complete: true);
            await AddStepAsync("C", complete: false);

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.CompletedSteps.Should().Be(2);
            stats.PendingSteps.Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_AllPending_CompletedIsZero()
        {
            await AddStepAsync("A");
            await AddStepAsync("B");

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.CompletedSteps.Should().Be(0);
            stats.PendingSteps.Should().Be(2);
        }

        // ── CompletionPercentage ──────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_HalfCompleted_Percentage50()
        {
            await AddStepAsync("A", complete: true);
            await AddStepAsync("B", complete: false);

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.CompletionPercentage.Should().BeApproximately(50.0, 0.01);
        }

        [Fact]
        public async Task GetStatsAsync_AllCompleted_Percentage100()
        {
            await AddStepAsync("A", complete: true);
            await AddStepAsync("B", complete: true);

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.CompletionPercentage.Should().BeApproximately(100.0, 0.01);
        }

        [Fact]
        public async Task GetStatsAsync_NoSteps_PercentageIsZero()
        {
            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.CompletionPercentage.Should().Be(0.0);
        }

        // ── OverdueSteps ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_TwoOverdueSteps_CountsCorrectly()
        {
            var past = DateTime.UtcNow.AddDays(-1);
            await AddStepAsync("A", dueDate: past);
            await AddStepAsync("B", dueDate: past);
            await AddStepAsync("C"); // no due date

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.OverdueSteps.Should().Be(2);
        }

        [Fact]
        public async Task GetStatsAsync_CompletedOverdueStep_NotCountedAsOverdue()
        {
            var past = DateTime.UtcNow.AddDays(-1);
            await AddStepAsync("A", complete: true, dueDate: past);

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.OverdueSteps.Should().Be(0);
        }

        // ── PriorityBreakdown ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_MixedPriorities_BreakdownIsCorrect()
        {
            await AddStepAsync("A", priority: StepPriority.High);
            await AddStepAsync("B", priority: StepPriority.High);
            await AddStepAsync("C", priority: StepPriority.Low);
            await AddStepAsync("D", priority: StepPriority.None);

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.PriorityBreakdown["High"].Should().Be(2);
            stats.PriorityBreakdown["Low"].Should().Be(1);
            stats.PriorityBreakdown["None"].Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_AllSamePriority_BreakdownHasSingleEntry()
        {
            await AddStepAsync("A", priority: StepPriority.Medium);
            await AddStepAsync("B", priority: StepPriority.Medium);

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.PriorityBreakdown.Should().HaveCount(1);
            stats.PriorityBreakdown["Medium"].Should().Be(2);
        }

        // ── TagBreakdown ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_WithTags_TagBreakdownIsCorrect()
        {
            await AddStepAsync("A", tags: new[] { "focus", "energy" });
            await AddStepAsync("B", tags: new[] { "focus" });
            await AddStepAsync("C", tags: new[] { "calm" });

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.TagBreakdown["focus"].Should().Be(2);
            stats.TagBreakdown["energy"].Should().Be(1);
            stats.TagBreakdown["calm"].Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_NoTags_TagBreakdownIsEmpty()
        {
            await AddStepAsync("A");
            await AddStepAsync("B");

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.TagBreakdown.Should().BeEmpty();
        }

        // ── Authorization ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_GoalNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _stepService.GetStatsAsync(Guid.NewGuid(), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task GetStatsAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            Func<Task> act = async () => await _stepService.GetStatsAsync(_goalId, Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        // ── Combined scenario ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_FullScenario_AllFieldsCorrect()
        {
            var past = DateTime.UtcNow.AddDays(-1);

            await AddStepAsync("A", complete: true,  priority: StepPriority.High,   tags: new[] { "core", "urgent" });
            await AddStepAsync("B", complete: true,  priority: StepPriority.High,   tags: new[] { "core" });
            await AddStepAsync("C", complete: false, priority: StepPriority.Medium, tags: new[] { "optional" }, dueDate: past);
            await AddStepAsync("D", complete: false, priority: StepPriority.None);

            var stats = await _stepService.GetStatsAsync(_goalId, _userId);

            stats.TotalSteps.Should().Be(4);
            stats.CompletedSteps.Should().Be(2);
            stats.PendingSteps.Should().Be(2);
            stats.OverdueSteps.Should().Be(1);
            stats.CompletionPercentage.Should().BeApproximately(50.0, 0.01);
            stats.PriorityBreakdown["High"].Should().Be(2);
            stats.PriorityBreakdown["Medium"].Should().Be(1);
            stats.PriorityBreakdown["None"].Should().Be(1);
            stats.TagBreakdown["core"].Should().Be(2);
            stats.TagBreakdown["urgent"].Should().Be(1);
            stats.TagBreakdown["optional"].Should().Be(1);
        }
    }
}
