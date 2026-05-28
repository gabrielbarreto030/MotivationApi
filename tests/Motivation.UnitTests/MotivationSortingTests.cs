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
    public class MotivationSortingTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly MotivationService _motivationService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _goalId;

        public MotivationSortingTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_MotivationSorting_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _motivationRepository = new MotivationRepository(_context, _cache);
            _motivationService = new MotivationService(_motivationRepository, _goalRepository, NullLogger<MotivationService>.Instance);

            _goalId = Guid.NewGuid();
            var goal = new Goal(_goalId, _userId, "Test Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            _goalRepository.AddAsync(goal).Wait();
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        // ── MotivationFilterRequest: sorting defaults ─────────────────────────

        [Fact]
        public void MotivationFilterRequest_DefaultSortBy_IsCreatedAt()
        {
            var req = new MotivationFilterRequest();
            req.SortBy.Should().Be("createdat");
            req.SortOrder.Should().Be("asc");
        }

        [Fact]
        public void MotivationFilterRequest_SortOrderDesc_StoresDesc()
        {
            var req = new MotivationFilterRequest(sortOrder: "desc");
            req.SortOrder.Should().Be("desc");
        }

        [Fact]
        public void MotivationFilterRequest_InvalidSortOrder_DefaultsToAsc()
        {
            var req = new MotivationFilterRequest(sortOrder: "random");
            req.SortOrder.Should().Be("asc");
        }

        [Fact]
        public void MotivationFilterRequest_SortByText_StoresText()
        {
            var req = new MotivationFilterRequest(sortBy: "text");
            req.SortBy.Should().Be("text");
        }

        [Fact]
        public void MotivationFilterRequest_SortByIsCaseInsensitive()
        {
            var req = new MotivationFilterRequest(sortBy: "TEXT");
            req.SortBy.Should().Be("text");
        }

        // ── Motivation entity: CreatedAt ──────────────────────────────────────

        [Fact]
        public void Motivation_Constructor_SetsCreatedAt_WhenProvided()
        {
            var expected = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), Guid.NewGuid(), "text", expected);
            m.CreatedAt.Should().Be(expected);
        }

        [Fact]
        public void Motivation_Constructor_SetsCreatedAtToUtcNow_WhenDefault()
        {
            var before = DateTime.UtcNow;
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), Guid.NewGuid(), "text");
            var after = DateTime.UtcNow;
            m.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        }

        // ── MotivationService: ListByGoalFilteredAsync sorting ────────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_DefaultSort_SortsByCreatedAtAsc()
        {
            var base1 = DateTime.UtcNow;
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Zebra phrase", base1.AddSeconds(2)));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Alpha phrase", base1));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Mango phrase", base1.AddSeconds(1)));

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, new MotivationFilterRequest());

            result.Items[0].Text.Should().Be("Alpha phrase");
            result.Items[1].Text.Should().Be("Mango phrase");
            result.Items[2].Text.Should().Be("Zebra phrase");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SortByCreatedAtDesc_ReturnsMostRecentFirst()
        {
            var base1 = DateTime.UtcNow;
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "First", base1));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Second", base1.AddSeconds(1)));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Third", base1.AddSeconds(2)));

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId,
                new MotivationFilterRequest(sortBy: "createdat", sortOrder: "desc"));

            result.Items[0].Text.Should().Be("Third");
            result.Items[1].Text.Should().Be("Second");
            result.Items[2].Text.Should().Be("First");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SortByTextAsc_ReturnsAlphabeticalOrder()
        {
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Zebra phrase"));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Alpha phrase"));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Mango phrase"));

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId,
                new MotivationFilterRequest(sortBy: "text", sortOrder: "asc"));

            result.Items.Select(m => m.Text).Should().BeInAscendingOrder();
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SortByTextDesc_ReturnsReverseAlphabeticalOrder()
        {
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Zebra phrase"));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Alpha phrase"));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Mango phrase"));

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId,
                new MotivationFilterRequest(sortBy: "text", sortOrder: "desc"));

            result.Items.Select(m => m.Text).Should().BeInDescendingOrder();
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SortAndSearch_WorkTogether()
        {
            var base1 = DateTime.UtcNow;
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Keep going B", base1.AddSeconds(1)));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Keep going A", base1));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Stay focused", base1.AddSeconds(2)));

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId,
                new MotivationFilterRequest(search: "Keep going", sortBy: "text", sortOrder: "asc"));

            result.TotalCount.Should().Be(2);
            result.Items[0].Text.Should().Be("Keep going A");
            result.Items[1].Text.Should().Be("Keep going B");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SortAndPagination_WorkTogether()
        {
            var base1 = DateTime.UtcNow;
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Zebra", base1.AddSeconds(2)));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Alpha", base1));
            await _motivationRepository.AddAsync(new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "Mango", base1.AddSeconds(1)));

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId,
                new MotivationFilterRequest(page: 1, pageSize: 2, sortBy: "text", sortOrder: "asc"));

            result.TotalCount.Should().Be(3);
            result.Items.Should().HaveCount(2);
            result.Items[0].Text.Should().Be("Alpha");
            result.Items[1].Text.Should().Be("Mango");
        }

        // ── AddMotivationResponse: CreatedAt included ─────────────────────────

        [Fact]
        public async Task AddAsync_ReturnsResponseWithCreatedAt()
        {
            var before = DateTime.UtcNow;
            var response = await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay strong"), _userId);
            var after = DateTime.UtcNow;

            response.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        }
    }
}
