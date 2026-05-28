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
    public class MotivationTagsTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly MotivationService _motivationService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _goalId;

        public MotivationTagsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_MotivationTags_" + Guid.NewGuid())
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

        // ── Domain: Motivation.SetTags ────────────────────────────────────────────

        [Fact]
        public void SetTags_WithValues_StoresTagsRaw()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            m.SetTags(new[] { "faith", "strength" });

            m.Tags.Should().BeEquivalentTo(new[] { "faith", "strength" });
        }

        [Fact]
        public void SetTags_Null_ClearsTags()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");
            m.SetTags(new[] { "faith" });

            m.SetTags(null);

            m.Tags.Should().BeEmpty();
            m.TagsRaw.Should().BeEmpty();
        }

        [Fact]
        public void SetTags_TrimsWhitespace_StoresCleanValues()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            m.SetTags(new[] { "  faith  ", "  strength " });

            m.Tags.Should().BeEquivalentTo(new[] { "faith", "strength" });
        }

        [Fact]
        public void SetTags_EmptyStrings_SkipsBlankEntries()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            m.SetTags(new[] { "faith", "", "   " });

            m.Tags.Should().BeEquivalentTo(new[] { "faith" });
        }

        [Fact]
        public void Motivation_DefaultTags_IsEmptyList()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            m.Tags.Should().BeEmpty();
            m.TagsRaw.Should().BeEmpty();
        }

        [Fact]
        public void Motivation_Constructor_WithTagsRaw_RestoresTags()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text", tagsRaw: "faith,strength");

            m.Tags.Should().BeEquivalentTo(new[] { "faith", "strength" });
        }

        // ── Application: AddAsync with Tags ───────────────────────────────────────

        [Fact]
        public async Task AddAsync_WithTags_ReturnsMotivationWithTags()
        {
            var request = new AddMotivationRequest("Stay strong", Tags: new[] { "faith", "resilience" });

            var result = await _motivationService.AddAsync(_goalId, request, _userId);

            result.Tags.Should().BeEquivalentTo(new[] { "faith", "resilience" });
        }

        [Fact]
        public async Task AddAsync_WithoutTags_ReturnsMotivationWithEmptyTags()
        {
            var request = new AddMotivationRequest("Keep going");

            var result = await _motivationService.AddAsync(_goalId, request, _userId);

            result.Tags.Should().BeNullOrEmpty();
        }

        // ── Application: UpdateAsync with Tags ────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_WithTags_UpdatesMotivationTags()
        {
            var added = await _motivationService.AddAsync(_goalId, new AddMotivationRequest("phrase"), _userId);

            var update = new UpdateMotivationRequest("phrase", Tags: new[] { "hope", "courage" });
            var result = await _motivationService.UpdateAsync(_goalId, added.Id, update, _userId);

            result.Tags.Should().BeEquivalentTo(new[] { "hope", "courage" });
        }

        [Fact]
        public async Task UpdateAsync_ClearTags_RemovesAllTags()
        {
            var added = await _motivationService.AddAsync(_goalId, new AddMotivationRequest("phrase", Tags: new[] { "faith" }), _userId);

            var update = new UpdateMotivationRequest("phrase", ClearTags: true);
            var result = await _motivationService.UpdateAsync(_goalId, added.Id, update, _userId);

            result.Tags.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task UpdateAsync_NoTagsParam_PreservesExistingTags()
        {
            var added = await _motivationService.AddAsync(_goalId, new AddMotivationRequest("phrase", Tags: new[] { "faith" }), _userId);

            var update = new UpdateMotivationRequest("Updated phrase");
            var result = await _motivationService.UpdateAsync(_goalId, added.Id, update, _userId);

            result.Tags.Should().BeEquivalentTo(new[] { "faith" });
        }

        // ── Application: ListByGoalFilteredAsync with tag filter ──────────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_FilterByTag_ReturnsMatchingMotivations()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay strong", Tags: new[] { "faith", "resilience" }), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Keep learning", Tags: new[] { "growth" }), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("No tag phrase"), _userId);

            var request = new MotivationFilterRequest(tag: "faith");
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Text.Should().Be("Stay strong");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_FilterByTag_IsCaseInsensitive()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay strong", Tags: new[] { "Faith" }), _userId);

            var request = new MotivationFilterRequest(tag: "faith");
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_FilterByTag_NoMatch_ReturnsEmpty()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay strong", Tags: new[] { "faith" }), _userId);

            var request = new MotivationFilterRequest(tag: "growth");
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_NullTag_ReturnsAllMotivations()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay strong", Tags: new[] { "faith" }), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Keep going"), _userId);

            var request = new MotivationFilterRequest(tag: null);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_TagAndSearch_CombinesFilters()
        {
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Keep going strong", Tags: new[] { "faith" }), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Keep going fast", Tags: new[] { "speed" }), _userId);
            await _motivationService.AddAsync(_goalId, new AddMotivationRequest("Stay focused", Tags: new[] { "faith" }), _userId);

            var request = new MotivationFilterRequest(search: "Keep going", tag: "faith");
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, request);

            result.Items.Should().HaveCount(1);
            result.Items[0].Text.Should().Be("Keep going strong");
        }

        // ── MotivationFilterRequest: Tag property ─────────────────────────────────

        [Fact]
        public void MotivationFilterRequest_NullTag_TagIsNull()
        {
            var req = new MotivationFilterRequest(tag: null);
            req.Tag.Should().BeNull();
        }

        [Fact]
        public void MotivationFilterRequest_WhitespaceTag_TagIsNull()
        {
            var req = new MotivationFilterRequest(tag: "   ");
            req.Tag.Should().BeNull();
        }

        [Fact]
        public void MotivationFilterRequest_ValidTag_IsTrimmed()
        {
            var req = new MotivationFilterRequest(tag: "  faith  ");
            req.Tag.Should().Be("faith");
        }
    }
}
