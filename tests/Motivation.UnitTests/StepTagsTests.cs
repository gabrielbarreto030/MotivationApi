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
    public class StepTagsTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _goalId;

        public StepTagsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_StepTags_" + Guid.NewGuid())
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

        // ── Domain: Step.SetTags ──────────────────────────────────────────────────

        [Fact]
        public void SetTags_WithValues_StoresTagsRaw()
        {
            var step = new Step(Guid.NewGuid(), _goalId, "T");

            step.SetTags(new[] { "cardio", "morning" });

            step.Tags.Should().BeEquivalentTo(new[] { "cardio", "morning" });
        }

        [Fact]
        public void SetTags_Null_ClearsTags()
        {
            var step = new Step(Guid.NewGuid(), _goalId, "T");
            step.SetTags(new[] { "cardio" });

            step.SetTags(null);

            step.Tags.Should().BeEmpty();
            step.TagsRaw.Should().BeEmpty();
        }

        [Fact]
        public void SetTags_TrimsWhitespace_StoresCleanValues()
        {
            var step = new Step(Guid.NewGuid(), _goalId, "T");

            step.SetTags(new[] { "  cardio  ", "  morning " });

            step.Tags.Should().BeEquivalentTo(new[] { "cardio", "morning" });
        }

        [Fact]
        public void SetTags_EmptyStrings_SkipsBlankEntries()
        {
            var step = new Step(Guid.NewGuid(), _goalId, "T");

            step.SetTags(new[] { "cardio", "", "   " });

            step.Tags.Should().BeEquivalentTo(new[] { "cardio" });
        }

        [Fact]
        public void Step_DefaultTags_IsEmptyList()
        {
            var step = new Step(Guid.NewGuid(), _goalId, "T");

            step.Tags.Should().BeEmpty();
            step.TagsRaw.Should().BeEmpty();
        }

        [Fact]
        public void Step_Constructor_WithTagsRaw_RestoresTags()
        {
            var step = new Step(Guid.NewGuid(), _goalId, "T", tagsRaw: "cardio,morning");

            step.Tags.Should().BeEquivalentTo(new[] { "cardio", "morning" });
        }

        // ── Application: CreateAsync with Tags ────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WithTags_ReturnsStepWithTags()
        {
            var request = new CreateStepRequest("Run 5km", Tags: new[] { "cardio", "morning" });

            var result = await _stepService.CreateAsync(_goalId, request, _userId);

            result.Tags.Should().BeEquivalentTo(new[] { "cardio", "morning" });
        }

        [Fact]
        public async Task CreateAsync_WithoutTags_ReturnsStepWithEmptyTags()
        {
            var request = new CreateStepRequest("Read book");

            var result = await _stepService.CreateAsync(_goalId, request, _userId);

            result.Tags.Should().BeNullOrEmpty();
        }

        // ── Application: UpdateAsync with Tags ────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_WithTags_UpdatesStepTags()
        {
            var created = await _stepService.CreateAsync(_goalId, new CreateStepRequest("Step"), _userId);

            var update = new UpdateStepRequest(Tags: new[] { "learning", "tech" });
            var result = await _stepService.UpdateAsync(_goalId, created.Id, update, _userId);

            result.Tags.Should().BeEquivalentTo(new[] { "learning", "tech" });
        }

        [Fact]
        public async Task UpdateAsync_ClearTags_RemovesAllTags()
        {
            var created = await _stepService.CreateAsync(_goalId, new CreateStepRequest("Step", Tags: new[] { "cardio" }), _userId);

            var update = new UpdateStepRequest(ClearTags: true);
            var result = await _stepService.UpdateAsync(_goalId, created.Id, update, _userId);

            result.Tags.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task UpdateAsync_NoTagsParam_PreservesExistingTags()
        {
            var created = await _stepService.CreateAsync(_goalId, new CreateStepRequest("Step", Tags: new[] { "cardio" }), _userId);

            var update = new UpdateStepRequest("Updated Title");
            var result = await _stepService.UpdateAsync(_goalId, created.Id, update, _userId);

            result.Tags.Should().BeEquivalentTo(new[] { "cardio" });
        }

        // ── Application: ListByGoalFilteredAsync with tag filter ──────────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_FilterByTag_ReturnsMatchingSteps()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run", Tags: new[] { "cardio", "morning" }), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read", Tags: new[] { "learning" }), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("No Tag"), _userId);

            var request = new StepFilterRequest(tag: "cardio");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Run");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_FilterByTag_IsCaseInsensitive()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run", Tags: new[] { "Cardio" }), _userId);

            var request = new StepFilterRequest(tag: "cardio");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_FilterByTag_NoMatch_ReturnsEmpty()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run", Tags: new[] { "cardio" }), _userId);

            var request = new StepFilterRequest(tag: "learning");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_NullTag_ReturnsAllSteps()
        {
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run", Tags: new[] { "cardio" }), _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read"), _userId);

            var request = new StepFilterRequest(tag: null);
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_TagAndIsCompletedFilter_CombinesFilters()
        {
            var step1 = await _stepService.CreateAsync(_goalId, new CreateStepRequest("Run", Tags: new[] { "cardio" }), _userId);
            var step2 = await _stepService.CreateAsync(_goalId, new CreateStepRequest("Walk", Tags: new[] { "cardio" }), _userId);
            await _stepService.MarkCompletedAsync(_goalId, step2.Id, _userId);
            await _stepService.CreateAsync(_goalId, new CreateStepRequest("Read", Tags: new[] { "learning" }), _userId);

            var request = new StepFilterRequest(isCompleted: false, tag: "cardio");
            var result = await _stepService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Run");
        }

        // ── Application: StepFilterRequest.Tag property ───────────────────────────

        [Fact]
        public void StepFilterRequest_NullTag_TagIsNull()
        {
            var request = new StepFilterRequest(tag: null);

            request.Tag.Should().BeNull();
        }

        [Fact]
        public void StepFilterRequest_WhitespaceTag_TagIsNull()
        {
            var request = new StepFilterRequest(tag: "   ");

            request.Tag.Should().BeNull();
        }

        [Fact]
        public void StepFilterRequest_ValidTag_IsTrimmed()
        {
            var request = new StepFilterRequest(tag: "  cardio  ");

            request.Tag.Should().Be("cardio");
        }
    }
}
