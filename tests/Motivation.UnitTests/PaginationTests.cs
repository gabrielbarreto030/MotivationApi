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
    public class PaginationTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly StepService _stepService;

        public PaginationTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_Pagination_" + Guid.NewGuid())
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

        // ── PagedRequest constraint tests ─────────────────────────────────────

        [Fact]
        public void PagedRequest_DefaultValues_AreCorrect()
        {
            var req = new PagedRequest();
            req.Page.Should().Be(1);
            req.PageSize.Should().Be(10);
        }

        [Fact]
        public void PagedRequest_ZeroPage_ClampedToOne()
        {
            var req = new PagedRequest(page: 0);
            req.Page.Should().Be(1);
        }

        [Fact]
        public void PagedRequest_NegativePage_ClampedToOne()
        {
            var req = new PagedRequest(page: -5);
            req.Page.Should().Be(1);
        }

        [Fact]
        public void PagedRequest_PageSizeExceedsMax_ClampedTo50()
        {
            var req = new PagedRequest(pageSize: 200);
            req.PageSize.Should().Be(50);
        }

        [Fact]
        public void PagedRequest_ZeroPageSize_ClampedToOne()
        {
            var req = new PagedRequest(pageSize: 0);
            req.PageSize.Should().Be(1);
        }

        // ── PagedResponse shape tests ─────────────────────────────────────────

        [Fact]
        public void PagedResponse_TotalPages_CalculatedCorrectly()
        {
            var items = new[] { "a", "b" };
            var response = new PagedResponse<string>(items, totalCount: 25, page: 1, pageSize: 10);
            response.TotalPages.Should().Be(3);
        }

        [Fact]
        public void PagedResponse_ExactDivision_TotalPagesExact()
        {
            var response = new PagedResponse<string>(Array.Empty<string>(), totalCount: 20, page: 1, pageSize: 10);
            response.TotalPages.Should().Be(2);
        }

        [Fact]
        public void PagedResponse_ZeroTotalCount_TotalPagesIsZero()
        {
            var response = new PagedResponse<string>(Array.Empty<string>(), totalCount: 0, page: 1, pageSize: 10);
            response.TotalPages.Should().Be(0);
        }

        // ── GoalService pagination tests ──────────────────────────────────────

        [Fact]
        public async Task GoalService_ListByUserPagedAsync_FirstPage_ReturnsCorrectItems()
        {
            var userId = Guid.NewGuid();
            for (int i = 1; i <= 15; i++)
                await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, $"Goal {i}", "desc", GoalStatus.Pending, DateTime.UtcNow));

            var result = await _goalService.ListByUserPagedAsync(userId, new PagedRequest(page: 1, pageSize: 10));

            result.Page.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(15);
            result.TotalPages.Should().Be(2);
            result.Items.Should().HaveCount(10);
        }

        [Fact]
        public async Task GoalService_ListByUserPagedAsync_SecondPage_ReturnsRemainingItems()
        {
            var userId = Guid.NewGuid();
            for (int i = 1; i <= 15; i++)
                await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, $"Goal {i}", "desc", GoalStatus.Pending, DateTime.UtcNow));

            var result = await _goalService.ListByUserPagedAsync(userId, new PagedRequest(page: 2, pageSize: 10));

            result.Page.Should().Be(2);
            result.TotalCount.Should().Be(15);
            result.Items.Should().HaveCount(5);
        }

        [Fact]
        public async Task GoalService_ListByUserPagedAsync_PageBeyondData_ReturnsEmpty()
        {
            var userId = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Goal 1", "desc", GoalStatus.Pending, DateTime.UtcNow));

            var result = await _goalService.ListByUserPagedAsync(userId, new PagedRequest(page: 5, pageSize: 10));

            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task GoalService_ListByUserPagedAsync_EmptyList_ReturnsPaged()
        {
            var userId = Guid.NewGuid();

            var result = await _goalService.ListByUserPagedAsync(userId, new PagedRequest(page: 1, pageSize: 10));

            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
            result.TotalPages.Should().Be(0);
        }

        [Fact]
        public async Task GoalService_ListByUserPagedAsync_DoesNotReturnOtherUsersGoals()
        {
            var userId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Mine", "desc", GoalStatus.Pending, DateTime.UtcNow));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), otherId, "Not mine", "desc", GoalStatus.Pending, DateTime.UtcNow));

            var result = await _goalService.ListByUserPagedAsync(userId, new PagedRequest());

            result.TotalCount.Should().Be(1);
            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Mine");
        }

        // ── StepService pagination tests ──────────────────────────────────────

        [Fact]
        public async Task StepService_ListByGoalPagedAsync_FirstPage_ReturnsCorrectItems()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            for (int i = 1; i <= 12; i++)
                await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, $"Step {i}"));

            var result = await _stepService.ListByGoalPagedAsync(goal.Id, userId, new PagedRequest(page: 1, pageSize: 5));

            result.Page.Should().Be(1);
            result.PageSize.Should().Be(5);
            result.TotalCount.Should().Be(12);
            result.TotalPages.Should().Be(3);
            result.Items.Should().HaveCount(5);
        }

        [Fact]
        public async Task StepService_ListByGoalPagedAsync_LastPage_ReturnsRemainingItems()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            for (int i = 1; i <= 12; i++)
                await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, $"Step {i}"));

            var result = await _stepService.ListByGoalPagedAsync(goal.Id, userId, new PagedRequest(page: 3, pageSize: 5));

            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(12);
        }

        [Fact]
        public async Task StepService_ListByGoalPagedAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            Func<Task> act = async () =>
                await _stepService.ListByGoalPagedAsync(Guid.NewGuid(), userId, new PagedRequest());
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task StepService_ListByGoalPagedAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var owner = Guid.NewGuid();
            var other = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), owner, "Goal", "desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            Func<Task> act = async () =>
                await _stepService.ListByGoalPagedAsync(goal.Id, other, new PagedRequest());
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }
}
