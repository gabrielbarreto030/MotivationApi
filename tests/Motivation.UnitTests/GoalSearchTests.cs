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
    public class GoalSearchTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly Guid _userId = Guid.NewGuid();

        public GoalSearchTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_GoalSearch_" + Guid.NewGuid())
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

        private async Task<Goal> SeedGoalAsync(string title, string description = "Description")
        {
            var goal = new Goal(Guid.NewGuid(), _userId, title, description, GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        // ── GoalFilterRequest: Search property ───────────────────────────────────

        [Fact]
        public void GoalFilterRequest_NullSearch_SearchIsNull()
        {
            var request = new GoalFilterRequest(search: null);

            request.Search.Should().BeNull();
        }

        [Fact]
        public void GoalFilterRequest_WhitespaceSearch_SearchIsNull()
        {
            var request = new GoalFilterRequest(search: "   ");

            request.Search.Should().BeNull();
        }

        [Fact]
        public void GoalFilterRequest_ValidSearch_IsTrimmed()
        {
            var request = new GoalFilterRequest(search: "  dotnet  ");

            request.Search.Should().Be("dotnet");
        }

        // ── Application: Search by title ─────────────────────────────────────────

        [Fact]
        public async Task ListByUserFilteredAsync_SearchMatchesTitle_ReturnsMatchingGoals()
        {
            await SeedGoalAsync("Learn .NET 8");
            await SeedGoalAsync("Exercise daily");

            var request = new GoalFilterRequest(search: "NET");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Learn .NET 8");
        }

        [Fact]
        public async Task ListByUserFilteredAsync_SearchMatchesDescription_ReturnsMatchingGoals()
        {
            await SeedGoalAsync("Goal A", "Master C# programming");
            await SeedGoalAsync("Goal B", "Improve fitness level");

            var request = new GoalFilterRequest(search: "C#");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Goal A");
        }

        [Fact]
        public async Task ListByUserFilteredAsync_SearchIsCaseInsensitive()
        {
            await SeedGoalAsync("Learn Azure");
            await SeedGoalAsync("Exercise daily");

            var request = new GoalFilterRequest(search: "azure");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Learn Azure");
        }

        [Fact]
        public async Task ListByUserFilteredAsync_SearchNoMatch_ReturnsEmpty()
        {
            await SeedGoalAsync("Learn .NET");
            await SeedGoalAsync("Exercise daily");

            var request = new GoalFilterRequest(search: "python");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task ListByUserFilteredAsync_SearchMatchesMultiple_ReturnsAll()
        {
            await SeedGoalAsync("Learn .NET 8", "Build a REST API with .NET");
            await SeedGoalAsync("Learn Azure", "Deploy .NET app to Azure");
            await SeedGoalAsync("Exercise daily", "Run 5km every morning");

            var request = new GoalFilterRequest(search: ".NET");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByUserFilteredAsync_NullSearch_ReturnsAllGoals()
        {
            await SeedGoalAsync("Goal A");
            await SeedGoalAsync("Goal B");

            var request = new GoalFilterRequest(search: null);
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByUserFilteredAsync_SearchWithStatusFilter_CombinesFilters()
        {
            var pending = new Goal(Guid.NewGuid(), _userId, "Learn .NET Pending", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            var inProgress = new Goal(Guid.NewGuid(), _userId, "Learn .NET InProgress", "Desc", GoalStatus.InProgress, DateTime.UtcNow);
            var other = new Goal(Guid.NewGuid(), _userId, "Exercise daily", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(pending);
            await _goalRepository.AddAsync(inProgress);
            await _goalRepository.AddAsync(other);

            var request = new GoalFilterRequest(status: GoalStatus.Pending, search: "Learn .NET");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Learn .NET Pending");
        }

        [Fact]
        public async Task ListByUserFilteredAsync_SearchDoesNotReturnOtherUserGoals()
        {
            var otherUserId = Guid.NewGuid();
            var ownGoal = new Goal(Guid.NewGuid(), _userId, "My .NET goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            var otherGoal = new Goal(Guid.NewGuid(), otherUserId, "Other .NET goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(ownGoal);
            await _goalRepository.AddAsync(otherGoal);

            var request = new GoalFilterRequest(search: ".NET");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Id.Should().Be(ownGoal.Id);
        }

        [Fact]
        public async Task ListByUserFilteredAsync_SearchIsPartialMatch()
        {
            await SeedGoalAsync("Aprender programação");

            var request = new GoalFilterRequest(search: "prog");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(1);
        }
    }
}
