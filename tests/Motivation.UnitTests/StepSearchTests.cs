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
    public class StepSearchTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _goalId;

        public StepSearchTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_StepSearch_" + Guid.NewGuid())
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

        // ── StepFilterRequest: Search property ───────────────────────────────────

        [Fact]
        public void StepFilterRequest_NullSearch_SearchIsNull()
        {
            var request = new StepFilterRequest(search: null);

            request.Search.Should().BeNull();
        }

        [Fact]
        public void StepFilterRequest_WhitespaceSearch_SearchIsNull()
        {
            var request = new StepFilterRequest(search: "   ");

            request.Search.Should().BeNull();
        }

        [Fact]
        public void StepFilterRequest_ValidSearch_IsTrimmed()
        {
            var request = new StepFilterRequest(search: "  run  ");

            request.Search.Should().Be("run");
        }

        // ── Application: Search by title ─────────────────────────────────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchMatchesTitle_ReturnsMatchingSteps()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 5km"), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read a book"), _userId);

            var request = new StepFilterRequest(search: "Run");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Run 5km");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchMatchesNotes_ReturnsMatchingSteps()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Step A", Notes: "Cardio training session"), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Step B", Notes: "Read chapter 5"), _userId);

            var request = new StepFilterRequest(search: "Cardio");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Step A");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchIsCaseInsensitive()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 5km"), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read book"), _userId);

            var request = new StepFilterRequest(search: "run");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Run 5km");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchIsPartialMatch()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Complete the assignment"), _userId);

            var request = new StepFilterRequest(search: "assign");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchNoMatch_ReturnsEmpty()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 5km"), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read book"), _userId);

            var request = new StepFilterRequest(search: "python");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_NullSearch_ReturnsAllSteps()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 5km"), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read book"), _userId);

            var request = new StepFilterRequest(search: null);
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchMatchesMultiple_ReturnsAll()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 5km"), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 10km"), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read book"), _userId);

            var request = new StepFilterRequest(search: "Run");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchAndIsCompletedFilter_CombinesFilters()
        {
            var step1 = await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 5km"), _userId);
            var step2 = await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 10km"), _userId);
            await _stepService.MarkCompletedAsync(_goalId, step2.Id, _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read book"), _userId);

            var request = new StepFilterRequest(isCompleted: false, search: "Run");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Run 5km");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchAndTagFilter_CombinesFilters()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 5km", Tags: new[] { "cardio" }), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 1km", Tags: new[] { "light" }), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read book"), _userId);

            var request = new StepFilterRequest(tag: "cardio", search: "Run");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Run 5km");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_SearchMatchesTitleOrNotes_ReturnsBoth()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Stretching", Notes: "Run warm-up"), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run 5km"), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read book"), _userId);

            var request = new StepFilterRequest(search: "run");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(2);
            result.Items.Should().Contain(s => s.Title == "Stretching");
            result.Items.Should().Contain(s => s.Title == "Run 5km");
        }
    }
}
