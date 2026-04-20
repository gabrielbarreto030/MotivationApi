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
    public class GoalNotesTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly Guid _userId = Guid.NewGuid();

        public GoalNotesTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Notes_" + Guid.NewGuid())
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

        // ── Domain: Notes property ───────────────────────────────────────────────

        [Fact]
        public void Goal_Constructor_DefaultNotes_IsNull()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.Notes.Should().BeNull();
        }

        [Fact]
        public void Goal_Constructor_WithNotes_SetsNotes()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow, null, GoalPriority.None, "My notes");

            goal.Notes.Should().Be("My notes");
        }

        [Fact]
        public void Goal_UpdateNotes_ChangesValue()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.UpdateNotes("Updated notes");

            goal.Notes.Should().Be("Updated notes");
        }

        [Fact]
        public void Goal_UpdateNotes_WithNull_ClearsNotes()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow, null, GoalPriority.None, "Existing notes");

            goal.UpdateNotes(null);

            goal.Notes.Should().BeNull();
        }

        [Fact]
        public void Goal_Update_WithNotes_SetsNotes()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.Update(null, null, null, null, false, null, "Added via Update");

            goal.Notes.Should().Be("Added via Update");
        }

        [Fact]
        public void Goal_Update_WithClearNotes_RemovesNotes()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow, null, GoalPriority.None, "Existing");

            goal.Update(null, null, null, null, false, null, null, clearNotes: true);

            goal.Notes.Should().BeNull();
        }

        [Fact]
        public void Goal_Update_WithNullNotes_KeepsExistingNotes()
        {
            var goal = new Goal(Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow, null, GoalPriority.None, "Keep me");

            goal.Update(null, null, null, null, false, null, null, false);

            goal.Notes.Should().Be("Keep me");
        }

        // ── Application: CreateAsync with notes ──────────────────────────────────

        [Fact]
        public async Task CreateAsync_WithNotes_ReturnsNotes()
        {
            var request = new CreateGoalRequest("Goal with notes", "Description", null, GoalPriority.None, "Important note");

            var result = await _goalService.CreateAsync(request, _userId);

            result.Notes.Should().Be("Important note");
        }

        [Fact]
        public async Task CreateAsync_WithoutNotes_ReturnsNullNotes()
        {
            var request = new CreateGoalRequest("Goal without notes", "Description");

            var result = await _goalService.CreateAsync(request, _userId);

            result.Notes.Should().BeNull();
        }

        // ── Application: UpdateAsync with notes ──────────────────────────────────

        [Fact]
        public async Task UpdateAsync_WithNotes_AddsNotes()
        {
            var created = await _goalService.CreateAsync(new CreateGoalRequest("Goal", "Desc"), _userId);

            var updateRequest = new UpdateGoalRequest(null, null, null, null, false, null, "New note");
            var updated = await _goalService.UpdateAsync(created.Id, updateRequest, _userId);

            updated.Notes.Should().Be("New note");
        }

        [Fact]
        public async Task UpdateAsync_WithClearNotes_RemovesNotes()
        {
            var created = await _goalService.CreateAsync(
                new CreateGoalRequest("Goal", "Desc", null, GoalPriority.None, "Original note"), _userId);

            var updateRequest = new UpdateGoalRequest(null, null, null, null, false, null, null, ClearNotes: true);
            var updated = await _goalService.UpdateAsync(created.Id, updateRequest, _userId);

            updated.Notes.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_WithNullNotes_KeepsExistingNotes()
        {
            var created = await _goalService.CreateAsync(
                new CreateGoalRequest("Goal", "Desc", null, GoalPriority.None, "Keep this note"), _userId);

            var updateRequest = new UpdateGoalRequest("Updated Title", null, null);
            var updated = await _goalService.UpdateAsync(created.Id, updateRequest, _userId);

            updated.Notes.Should().Be("Keep this note");
        }

        // ── Application: Notes preserved through list operations ─────────────────

        [Fact]
        public async Task ListByUserFilteredAsync_GoalWithNotes_ReturnsNotesInResponse()
        {
            await _goalService.CreateAsync(
                new CreateGoalRequest("Noted Goal", "Desc", null, GoalPriority.None, "My important context"), _userId);

            var filter = new GoalFilterRequest();
            var result = await _goalService.ListByUserFilteredAsync(_userId, filter);

            result.Items.Should().ContainSingle();
            result.Items[0].Notes.Should().Be("My important context");
        }

        [Fact]
        public async Task ListByUserAsync_GoalWithNotes_ReturnsNotesInResponse()
        {
            await _goalService.CreateAsync(
                new CreateGoalRequest("Noted Goal", "Desc", null, GoalPriority.None, "List note"), _userId);

            var goals = await _goalService.ListByUserAsync(_userId);

            goals.Should().ContainSingle();
            goals[0].Notes.Should().Be("List note");
        }
    }
}
