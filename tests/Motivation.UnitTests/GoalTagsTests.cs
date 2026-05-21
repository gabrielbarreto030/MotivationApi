using System;
using System.Collections.Generic;
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
    public class GoalTagsTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly Guid _userId = Guid.NewGuid();

        public GoalTagsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_GoalTags_" + Guid.NewGuid())
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

        // ── Domain: Goal.SetTags ──────────────────────────────────────────────────

        [Fact]
        public void SetTags_WithValues_StoresTagsRaw()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);

            goal.SetTags(new[] { "health", "fitness" });

            goal.Tags.Should().BeEquivalentTo(new[] { "health", "fitness" });
        }

        [Fact]
        public void SetTags_Null_ClearsTags()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);
            goal.SetTags(new[] { "health" });

            goal.SetTags(null);

            goal.Tags.Should().BeEmpty();
            goal.TagsRaw.Should().BeEmpty();
        }

        [Fact]
        public void SetTags_TrimsWhitespace_StoresCleanValues()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);

            goal.SetTags(new[] { "  health  ", "  fitness " });

            goal.Tags.Should().BeEquivalentTo(new[] { "health", "fitness" });
        }

        [Fact]
        public void SetTags_EmptyStrings_SkipsBlankEntries()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);

            goal.SetTags(new[] { "health", "", "   " });

            goal.Tags.Should().BeEquivalentTo(new[] { "health" });
        }

        [Fact]
        public void Goal_DefaultTags_IsEmptyList()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);

            goal.Tags.Should().BeEmpty();
            goal.TagsRaw.Should().BeEmpty();
        }

        [Fact]
        public void Goal_Constructor_WithTagsRaw_RestoresTags()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow, tagsRaw: "health,fitness");

            goal.Tags.Should().BeEquivalentTo(new[] { "health", "fitness" });
        }

        // ── Domain: Goal.Update with tags ─────────────────────────────────────────

        [Fact]
        public void Update_WithTags_SetsTags()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);

            goal.Update(null, null, null, tags: new[] { "learning", "tech" });

            goal.Tags.Should().BeEquivalentTo(new[] { "learning", "tech" });
        }

        [Fact]
        public void Update_ClearTags_RemovesTags()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);
            goal.SetTags(new[] { "learning" });

            goal.Update(null, null, null, clearTags: true);

            goal.Tags.Should().BeEmpty();
        }

        [Fact]
        public void Update_NullTagsNoClear_DoesNotChangeTags()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "T", "D", GoalStatus.Pending, DateTime.UtcNow);
            goal.SetTags(new[] { "health" });

            goal.Update(null, null, null);

            goal.Tags.Should().BeEquivalentTo(new[] { "health" });
        }

        // ── Application: CreateAsync with Tags ────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WithTags_ReturnsGoalWithTags()
        {
            var request = new CreateGoalRequest("Learn .NET", "Description", Tags: new[] { "tech", "learning" });

            var result = await _goalService.CreateAsync(request, _userId);

            result.Tags.Should().BeEquivalentTo(new[] { "tech", "learning" });
        }

        [Fact]
        public async Task CreateAsync_WithoutTags_ReturnsGoalWithEmptyTags()
        {
            var request = new CreateGoalRequest("Learn .NET", "Description");

            var result = await _goalService.CreateAsync(request, _userId);

            result.Tags.Should().BeEmpty();
        }

        // ── Application: UpdateAsync with Tags ────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_WithTags_UpdatesGoalTags()
        {
            var created = await _goalService.CreateAsync(
                new CreateGoalRequest("Goal", "Desc"), _userId);

            var update = new UpdateGoalRequest(null, null, null, Tags: new[] { "health", "sport" });
            var result = await _goalService.UpdateAsync(created.Id, update, _userId);

            result.Tags.Should().BeEquivalentTo(new[] { "health", "sport" });
        }

        [Fact]
        public async Task UpdateAsync_ClearTags_RemovesAllTags()
        {
            var created = await _goalService.CreateAsync(
                new CreateGoalRequest("Goal", "Desc", Tags: new[] { "health" }), _userId);

            var update = new UpdateGoalRequest(null, null, null, ClearTags: true);
            var result = await _goalService.UpdateAsync(created.Id, update, _userId);

            result.Tags.Should().BeEmpty();
        }

        [Fact]
        public async Task UpdateAsync_NoTagsParam_PreservesExistingTags()
        {
            var created = await _goalService.CreateAsync(
                new CreateGoalRequest("Goal", "Desc", Tags: new[] { "health" }), _userId);

            var update = new UpdateGoalRequest("Updated Title", null, null);
            var result = await _goalService.UpdateAsync(created.Id, update, _userId);

            result.Tags.Should().BeEquivalentTo(new[] { "health" });
        }

        // ── Application: ListByUserFilteredAsync with tag filter ──────────────────

        [Fact]
        public async Task ListByUserFilteredAsync_FilterByTag_ReturnsMatchingGoals()
        {
            await _goalService.CreateAsync(
                new CreateGoalRequest("Health Goal", "Desc", Tags: new[] { "health", "sport" }), _userId);
            await _goalService.CreateAsync(
                new CreateGoalRequest("Tech Goal", "Desc", Tags: new[] { "tech" }), _userId);
            await _goalService.CreateAsync(
                new CreateGoalRequest("No Tag Goal", "Desc"), _userId);

            var request = new GoalFilterRequest(tag: "health");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Health Goal");
        }

        [Fact]
        public async Task ListByUserFilteredAsync_FilterByTag_IsCaseInsensitive()
        {
            await _goalService.CreateAsync(
                new CreateGoalRequest("Goal", "Desc", Tags: new[] { "Health" }), _userId);

            var request = new GoalFilterRequest(tag: "health");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task ListByUserFilteredAsync_FilterByTag_NoMatch_ReturnsEmpty()
        {
            await _goalService.CreateAsync(
                new CreateGoalRequest("Goal", "Desc", Tags: new[] { "tech" }), _userId);

            var request = new GoalFilterRequest(tag: "health");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task ListByUserFilteredAsync_NullTag_ReturnsAllGoals()
        {
            await _goalService.CreateAsync(new CreateGoalRequest("A", "D", Tags: new[] { "tech" }), _userId);
            await _goalService.CreateAsync(new CreateGoalRequest("B", "D"), _userId);

            var request = new GoalFilterRequest(tag: null);
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByUserFilteredAsync_TagAndStatusFilter_CombinesFilters()
        {
            await _goalService.CreateAsync(
                new CreateGoalRequest("Pending tech", "D", Tags: new[] { "tech" }), _userId);

            var inProgressGoal = new Goal(Guid.NewGuid(), _userId, "InProgress tech", "D", GoalStatus.InProgress, DateTime.UtcNow);
            inProgressGoal.SetTags(new[] { "tech" });
            await _goalRepository.AddAsync(inProgressGoal);

            await _goalService.CreateAsync(
                new CreateGoalRequest("Pending other", "D", Tags: new[] { "sport" }), _userId);

            var request = new GoalFilterRequest(status: GoalStatus.Pending, tag: "tech");
            var result = await _goalService.ListByUserFilteredAsync(_userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Pending tech");
        }

        // ── Application: GoalFilterRequest.Tag property ───────────────────────────

        [Fact]
        public void GoalFilterRequest_NullTag_TagIsNull()
        {
            var request = new GoalFilterRequest(tag: null);

            request.Tag.Should().BeNull();
        }

        [Fact]
        public void GoalFilterRequest_WhitespaceTag_TagIsNull()
        {
            var request = new GoalFilterRequest(tag: "   ");

            request.Tag.Should().BeNull();
        }

        [Fact]
        public void GoalFilterRequest_ValidTag_IsTrimmed()
        {
            var request = new GoalFilterRequest(tag: "  health  ");

            request.Tag.Should().Be("health");
        }
    }
}
