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
    public class FilterTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly StepService _stepService;

        public FilterTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_Filter_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _goalService = new GoalService(_goalRepository, _stepRepository, NullLogger<GoalService>.Instance);
            _stepService = new StepService(_stepRepository, _goalRepository, NullLogger<StepService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        // ── GoalFilterRequest tests ───────────────────────────────────────────

        [Fact]
        public void GoalFilterRequest_DefaultValues_StatusIsNull()
        {
            var req = new GoalFilterRequest();
            req.Status.Should().BeNull();
            req.Page.Should().Be(1);
            req.PageSize.Should().Be(10);
        }

        [Fact]
        public void GoalFilterRequest_WithStatus_StoresCorrectly()
        {
            var req = new GoalFilterRequest(status: GoalStatus.InProgress);
            req.Status.Should().Be(GoalStatus.InProgress);
        }

        // ── StepFilterRequest tests ───────────────────────────────────────────

        [Fact]
        public void StepFilterRequest_DefaultValues_IsCompletedIsNull()
        {
            var req = new StepFilterRequest();
            req.IsCompleted.Should().BeNull();
            req.Page.Should().Be(1);
            req.PageSize.Should().Be(10);
        }

        [Fact]
        public void StepFilterRequest_WithIsCompleted_StoresCorrectly()
        {
            var req = new StepFilterRequest(isCompleted: true);
            req.IsCompleted.Should().BeTrue();
        }

        // ── GoalService filter tests ──────────────────────────────────────────

        [Fact]
        public async Task GoalService_ListByUserFilteredAsync_NoFilter_ReturnsAll()
        {
            var userId = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G1", "d", GoalStatus.Pending, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G2", "d", GoalStatus.InProgress, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G3", "d", GoalStatus.Completed, DateTime.UtcNow));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest());

            result.TotalCount.Should().Be(3);
            result.Items.Should().HaveCount(3);
        }

        [Fact]
        public async Task GoalService_ListByUserFilteredAsync_FilterByPending_ReturnsOnlyPending()
        {
            var userId = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G1", "d", GoalStatus.Pending, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G2", "d", GoalStatus.Pending, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G3", "d", GoalStatus.InProgress, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G4", "d", GoalStatus.Completed, DateTime.UtcNow));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(status: GoalStatus.Pending));

            result.TotalCount.Should().Be(2);
            result.Items.Should().HaveCount(2);
            result.Items.Should().OnlyContain(g => g.Status == GoalStatus.Pending);
        }

        [Fact]
        public async Task GoalService_ListByUserFilteredAsync_FilterByCompleted_ReturnsOnlyCompleted()
        {
            var userId = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G1", "d", GoalStatus.Pending, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G2", "d", GoalStatus.Completed, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G3", "d", GoalStatus.Completed, DateTime.UtcNow));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(status: GoalStatus.Completed));

            result.TotalCount.Should().Be(2);
            result.Items.Should().OnlyContain(g => g.Status == GoalStatus.Completed);
        }

        [Fact]
        public async Task GoalService_ListByUserFilteredAsync_FilterYieldsNoMatch_ReturnsEmpty()
        {
            var userId = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G1", "d", GoalStatus.Pending, DateTime.UtcNow));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(status: GoalStatus.Cancelled));

            result.TotalCount.Should().Be(0);
            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task GoalService_ListByUserFilteredAsync_FilterAndPagination_WorkTogether()
        {
            var userId = Guid.NewGuid();
            for (int i = 0; i < 8; i++)
                await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, $"P{i}", "d", GoalStatus.Pending, DateTime.UtcNow));
            for (int i = 0; i < 3; i++)
                await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, $"C{i}", "d", GoalStatus.Completed, DateTime.UtcNow));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(page: 1, pageSize: 5, status: GoalStatus.Pending));

            result.TotalCount.Should().Be(8);
            result.TotalPages.Should().Be(2);
            result.Items.Should().HaveCount(5);
            result.Items.Should().OnlyContain(g => g.Status == GoalStatus.Pending);
        }

        [Fact]
        public async Task GoalService_ListByUserFilteredAsync_DoesNotReturnOtherUsersGoals()
        {
            var userId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Mine", "d", GoalStatus.Pending, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), otherId, "NotMine", "d", GoalStatus.Pending, DateTime.UtcNow));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(status: GoalStatus.Pending));

            result.TotalCount.Should().Be(1);
            result.Items[0].Title.Should().Be("Mine");
        }

        // ── StepService filter tests ──────────────────────────────────────────

        [Fact]
        public async Task StepService_ListByGoalFilteredAsync_NoFilter_ReturnsAll()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var s1 = new Step(Guid.NewGuid(), goal.Id, "Step 1");
            var s2 = new Step(Guid.NewGuid(), goal.Id, "Step 2");
            s2.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.AddAsync(s1);
            await _stepRepository.AddAsync(s2);

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId, new StepFilterRequest());

            result.TotalCount.Should().Be(2);
            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task StepService_ListByGoalFilteredAsync_FilterCompleted_ReturnsOnlyCompleted()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var s1 = new Step(Guid.NewGuid(), goal.Id, "Pending step");
            var s2 = new Step(Guid.NewGuid(), goal.Id, "Done step");
            s2.MarkCompleted(DateTime.UtcNow);
            var s3 = new Step(Guid.NewGuid(), goal.Id, "Done step 2");
            s3.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.AddAsync(s1);
            await _stepRepository.AddAsync(s2);
            await _stepRepository.AddAsync(s3);

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId, new StepFilterRequest(isCompleted: true));

            result.TotalCount.Should().Be(2);
            result.Items.Should().OnlyContain(s => s.IsCompleted);
        }

        [Fact]
        public async Task StepService_ListByGoalFilteredAsync_FilterPending_ReturnsOnlyPending()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var s1 = new Step(Guid.NewGuid(), goal.Id, "Pending");
            var s2 = new Step(Guid.NewGuid(), goal.Id, "Done");
            s2.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.AddAsync(s1);
            await _stepRepository.AddAsync(s2);

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId, new StepFilterRequest(isCompleted: false));

            result.TotalCount.Should().Be(1);
            result.Items.Should().OnlyContain(s => !s.IsCompleted);
        }

        [Fact]
        public async Task StepService_ListByGoalFilteredAsync_FilterAndPagination_WorkTogether()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            for (int i = 0; i < 7; i++)
                await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, $"Pending {i}"));

            for (int i = 0; i < 3; i++)
            {
                var s = new Step(Guid.NewGuid(), goal.Id, $"Done {i}");
                s.MarkCompleted(DateTime.UtcNow);
                await _stepRepository.AddAsync(s);
            }

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId, new StepFilterRequest(page: 1, pageSize: 4, isCompleted: false));

            result.TotalCount.Should().Be(7);
            result.TotalPages.Should().Be(2);
            result.Items.Should().HaveCount(4);
            result.Items.Should().OnlyContain(s => !s.IsCompleted);
        }

        [Fact]
        public async Task StepService_ListByGoalFilteredAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            Func<Task> act = async () =>
                await _stepService.ListByGoalFilteredAsync(Guid.NewGuid(), userId, new StepFilterRequest());
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task StepService_ListByGoalFilteredAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var owner = Guid.NewGuid();
            var other = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), owner, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            Func<Task> act = async () =>
                await _stepService.ListByGoalFilteredAsync(goal.Id, other, new StepFilterRequest());
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }
}
