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
    public class SortingTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly StepService _stepService;

        public SortingTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_Sorting_" + Guid.NewGuid())
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

        // ── GoalFilterRequest sorting defaults ───────────────────────────────

        [Fact]
        public void GoalFilterRequest_DefaultSortBy_IsCreatedAt()
        {
            var req = new GoalFilterRequest();
            req.SortBy.Should().Be("createdat");
            req.SortOrder.Should().Be("asc");
        }

        [Fact]
        public void GoalFilterRequest_SortOrderDesc_StoresDesc()
        {
            var req = new GoalFilterRequest(sortOrder: "desc");
            req.SortOrder.Should().Be("desc");
        }

        [Fact]
        public void GoalFilterRequest_InvalidSortOrder_DefaultsToAsc()
        {
            var req = new GoalFilterRequest(sortOrder: "random");
            req.SortOrder.Should().Be("asc");
        }

        [Fact]
        public void GoalFilterRequest_SortByTitle_StoresTitle()
        {
            var req = new GoalFilterRequest(sortBy: "title");
            req.SortBy.Should().Be("title");
        }

        [Fact]
        public void GoalFilterRequest_SortByIsCaseInsensitive()
        {
            var req = new GoalFilterRequest(sortBy: "TITLE");
            req.SortBy.Should().Be("title");
        }

        // ── StepFilterRequest sorting defaults ───────────────────────────────

        [Fact]
        public void StepFilterRequest_DefaultSortBy_IsTitle()
        {
            var req = new StepFilterRequest();
            req.SortBy.Should().Be("title");
            req.SortOrder.Should().Be("asc");
        }

        [Fact]
        public void StepFilterRequest_SortOrderDesc_StoresDesc()
        {
            var req = new StepFilterRequest(sortOrder: "desc");
            req.SortOrder.Should().Be("desc");
        }

        [Fact]
        public void StepFilterRequest_SortByIsCompleted_StoresIsCompleted()
        {
            var req = new StepFilterRequest(sortBy: "isCompleted");
            req.SortBy.Should().Be("iscompleted");
        }

        // ── GoalService sorting tests ─────────────────────────────────────────

        [Fact]
        public async Task GoalService_ListFiltered_SortByTitleAsc_ReturnsSortedAscending()
        {
            var userId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Zebra", "d", GoalStatus.Pending, baseTime));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Alpha", "d", GoalStatus.Pending, baseTime.AddSeconds(1)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Mango", "d", GoalStatus.Pending, baseTime.AddSeconds(2)));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(sortBy: "title", sortOrder: "asc"));

            result.Items.Select(g => g.Title).Should().BeInAscendingOrder();
        }

        [Fact]
        public async Task GoalService_ListFiltered_SortByTitleDesc_ReturnsSortedDescending()
        {
            var userId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Zebra", "d", GoalStatus.Pending, baseTime));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Alpha", "d", GoalStatus.Pending, baseTime.AddSeconds(1)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Mango", "d", GoalStatus.Pending, baseTime.AddSeconds(2)));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(sortBy: "title", sortOrder: "desc"));

            result.Items.Select(g => g.Title).Should().BeInDescendingOrder();
        }

        [Fact]
        public async Task GoalService_ListFiltered_SortByStatusAsc_ReturnsSortedByStatusAscending()
        {
            var userId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G1", "d", GoalStatus.Completed, baseTime));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G2", "d", GoalStatus.Pending, baseTime.AddSeconds(1)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G3", "d", GoalStatus.InProgress, baseTime.AddSeconds(2)));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(sortBy: "status", sortOrder: "asc"));

            var statuses = result.Items.Select(g => (int)g.Status).ToArray();
            statuses.Should().BeInAscendingOrder();
        }

        [Fact]
        public async Task GoalService_ListFiltered_SortByStatusDesc_ReturnsSortedByStatusDescending()
        {
            var userId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G1", "d", GoalStatus.Pending, baseTime));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G2", "d", GoalStatus.Completed, baseTime.AddSeconds(1)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "G3", "d", GoalStatus.InProgress, baseTime.AddSeconds(2)));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(sortBy: "status", sortOrder: "desc"));

            var statuses = result.Items.Select(g => (int)g.Status).ToArray();
            statuses.Should().BeInDescendingOrder();
        }

        [Fact]
        public async Task GoalService_ListFiltered_SortByCreatedAtAsc_ReturnsSortedByDateAscending()
        {
            var userId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Third", "d", GoalStatus.Pending, baseTime.AddDays(2)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "First", "d", GoalStatus.Pending, baseTime));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Second", "d", GoalStatus.Pending, baseTime.AddDays(1)));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(sortBy: "createdat", sortOrder: "asc"));

            result.Items[0].Title.Should().Be("First");
            result.Items[1].Title.Should().Be("Second");
            result.Items[2].Title.Should().Be("Third");
        }

        [Fact]
        public async Task GoalService_ListFiltered_SortByCreatedAtDesc_ReturnsSortedByDateDescending()
        {
            var userId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Third", "d", GoalStatus.Pending, baseTime.AddDays(2)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "First", "d", GoalStatus.Pending, baseTime));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Second", "d", GoalStatus.Pending, baseTime.AddDays(1)));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest(sortBy: "createdat", sortOrder: "desc"));

            result.Items[0].Title.Should().Be("Third");
            result.Items[1].Title.Should().Be("Second");
            result.Items[2].Title.Should().Be("First");
        }

        [Fact]
        public async Task GoalService_ListFiltered_DefaultSort_SortsByCreatedAtAsc()
        {
            var userId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Last", "d", GoalStatus.Pending, baseTime.AddDays(1)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "First", "d", GoalStatus.Pending, baseTime));

            var result = await _goalService.ListByUserFilteredAsync(userId, new GoalFilterRequest());

            result.Items[0].Title.Should().Be("First");
            result.Items[1].Title.Should().Be("Last");
        }

        [Fact]
        public async Task GoalService_ListFiltered_SortAndFilter_WorkTogether()
        {
            var userId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Beta", "d", GoalStatus.Pending, baseTime));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Alpha", "d", GoalStatus.Pending, baseTime.AddSeconds(1)));
            await _goalRepository.AddAsync(new Goal(Guid.NewGuid(), userId, "Zeta", "d", GoalStatus.Completed, baseTime.AddSeconds(2)));

            var result = await _goalService.ListByUserFilteredAsync(userId,
                new GoalFilterRequest(status: GoalStatus.Pending, sortBy: "title", sortOrder: "asc"));

            result.TotalCount.Should().Be(2);
            result.Items[0].Title.Should().Be("Alpha");
            result.Items[1].Title.Should().Be("Beta");
        }

        // ── StepService sorting tests ─────────────────────────────────────────

        [Fact]
        public async Task StepService_ListFiltered_SortByTitleAsc_ReturnsSortedAscending()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, "Zebra step"));
            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, "Alpha step"));
            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, "Mango step"));

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId,
                new StepFilterRequest(sortBy: "title", sortOrder: "asc"));

            result.Items.Select(s => s.Title).Should().BeInAscendingOrder();
        }

        [Fact]
        public async Task StepService_ListFiltered_SortByTitleDesc_ReturnsSortedDescending()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, "Zebra step"));
            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, "Alpha step"));
            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, "Mango step"));

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId,
                new StepFilterRequest(sortBy: "title", sortOrder: "desc"));

            result.Items.Select(s => s.Title).Should().BeInDescendingOrder();
        }

        [Fact]
        public async Task StepService_ListFiltered_SortByIsCompletedAsc_PendingFirst()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var done = new Step(Guid.NewGuid(), goal.Id, "Done step");
            done.MarkCompleted(DateTime.UtcNow);
            var pending = new Step(Guid.NewGuid(), goal.Id, "Pending step");
            await _stepRepository.AddAsync(done);
            await _stepRepository.AddAsync(pending);

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId,
                new StepFilterRequest(sortBy: "iscompleted", sortOrder: "asc"));

            result.Items[0].IsCompleted.Should().BeFalse();
            result.Items[1].IsCompleted.Should().BeTrue();
        }

        [Fact]
        public async Task StepService_ListFiltered_SortByIsCompletedDesc_CompletedFirst()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var done = new Step(Guid.NewGuid(), goal.Id, "Done step");
            done.MarkCompleted(DateTime.UtcNow);
            var pending = new Step(Guid.NewGuid(), goal.Id, "Pending step");
            await _stepRepository.AddAsync(done);
            await _stepRepository.AddAsync(pending);

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId,
                new StepFilterRequest(sortBy: "iscompleted", sortOrder: "desc"));

            result.Items[0].IsCompleted.Should().BeTrue();
            result.Items[1].IsCompleted.Should().BeFalse();
        }

        [Fact]
        public async Task StepService_ListFiltered_DefaultSort_SortsByTitleAsc()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, "Zeta"));
            await _stepRepository.AddAsync(new Step(Guid.NewGuid(), goal.Id, "Alpha"));

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId, new StepFilterRequest());

            result.Items[0].Title.Should().Be("Alpha");
            result.Items[1].Title.Should().Be("Zeta");
        }

        [Fact]
        public async Task StepService_ListFiltered_SortAndFilter_WorkTogether()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Goal", "d", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var s1 = new Step(Guid.NewGuid(), goal.Id, "Zebra");
            var s2 = new Step(Guid.NewGuid(), goal.Id, "Alpha");
            var s3 = new Step(Guid.NewGuid(), goal.Id, "Mango");
            s3.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.AddAsync(s1);
            await _stepRepository.AddAsync(s2);
            await _stepRepository.AddAsync(s3);

            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, userId,
                new StepFilterRequest(isCompleted: false, sortBy: "title", sortOrder: "asc"));

            result.TotalCount.Should().Be(2);
            result.Items[0].Title.Should().Be("Alpha");
            result.Items[1].Title.Should().Be("Zebra");
            result.Items.Should().OnlyContain(s => !s.IsCompleted);
        }
    }
}
