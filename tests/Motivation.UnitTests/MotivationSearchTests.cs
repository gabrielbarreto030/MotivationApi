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
    public class MotivationSearchTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly MotivationService _motivationService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _goalId;

        public MotivationSearchTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_MotivationSearch_" + Guid.NewGuid())
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

        // ── MotivationFilterRequest: Search property ──────────────────────────────

        [Fact]
        public void MotivationFilterRequest_NullSearch_SearchIsNull()
        {
            var request = new MotivationFilterRequest(search: null);

            request.Search.Should().BeNull();
        }

        [Fact]
        public void MotivationFilterRequest_WhitespaceSearch_SearchIsNull()
        {
            var request = new MotivationFilterRequest(search: "   ");

            request.Search.Should().BeNull();
        }

        [Fact]
        public void MotivationFilterRequest_ValidSearch_IsTrimmed()
        {
            var request = new MotivationFilterRequest(search: "  keep  ");

            request.Search.Should().Be("keep");
        }

        // ── Application: ListByGoalFilteredAsync ─────────────────────────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_NullSearch_ReturnsAllMotivations()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Keep going!"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay focused"), _userId);

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, new MotivationFilterRequest());

            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(2);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchMatchesText_ReturnsMatchingMotivations()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Keep going!"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay focused"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Never give up"), _userId);

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, new MotivationFilterRequest(search: "Keep"));

            result.Items.Should().HaveCount(1);
            result.Items[0].Text.Should().Be("Keep going!");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchIsCaseInsensitive()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Keep going!"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay focused"), _userId);

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, new MotivationFilterRequest(search: "keep"));

            result.Items.Should().HaveCount(1);
            result.Items[0].Text.Should().Be("Keep going!");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchIsPartialMatch()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Consistency is key"), _userId);

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, new MotivationFilterRequest(search: "consis"));

            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchNoMatch_ReturnsEmpty()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Keep going!"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay focused"), _userId);

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, new MotivationFilterRequest(search: "python"));

            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchMatchesMultiple_ReturnsAll()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Believe in yourself"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Believe you can"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay focused"), _userId);

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, new MotivationFilterRequest(search: "believe"));

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_GoalNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _motivationService.ListByGoalFilteredAsync(
                Guid.NewGuid(), _userId, new MotivationFilterRequest());

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            Func<Task> act = async () => await _motivationService.ListByGoalFilteredAsync(
                _goalId, Guid.NewGuid(), new MotivationFilterRequest());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_PaginationWorks()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Phrase one"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Phrase two"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Phrase three"), _userId);

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, new MotivationFilterRequest(page: 1, pageSize: 2));

            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(3);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchAndPagination_CombinesCorrectly()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Go forward A"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Go forward B"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Go forward C"), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay still"), _userId);

            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId,
                new MotivationFilterRequest(page: 1, pageSize: 2, search: "Go forward"));

            result.TotalCount.Should().Be(3);
            result.Items.Should().HaveCount(2);
        }
    }
}
