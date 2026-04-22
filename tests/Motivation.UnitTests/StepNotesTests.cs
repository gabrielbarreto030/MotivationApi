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
    public class StepNotesTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;
        private readonly Guid _userId = Guid.NewGuid();

        public StepNotesTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_StepNotes_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _stepRepository = new StepRepository(_context, _cache);
            _stepService = new StepService(_stepRepository, _goalRepository, NullLogger<StepService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> CreateGoalAsync()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Test Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        // ── Domain: Notes property ───────────────────────────────────────────────

        [Fact]
        public void Step_Constructor_DefaultNotes_IsNull()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");

            step.Notes.Should().BeNull();
        }

        [Fact]
        public void Step_Constructor_WithNotes_SetsNotes()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", "My step note");

            step.Notes.Should().Be("My step note");
        }

        [Fact]
        public void Step_UpdateNotes_ChangesValue()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");

            step.UpdateNotes("Updated note");

            step.Notes.Should().Be("Updated note");
        }

        [Fact]
        public void Step_UpdateNotes_WithNull_ClearsNotes()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title", "Existing note");

            step.UpdateNotes(null);

            step.Notes.Should().BeNull();
        }

        // ── Application: CreateAsync with notes ──────────────────────────────────

        [Fact]
        public async Task CreateAsync_WithNotes_ReturnsNotes()
        {
            var goal = await CreateGoalAsync();
            var request = new CreateStepRequest("Step with note", "Important context");

            var result = await _stepService.CreateAsync(goal.Id, request, _userId);

            result.Notes.Should().Be("Important context");
        }

        [Fact]
        public async Task CreateAsync_WithoutNotes_ReturnsNullNotes()
        {
            var goal = await CreateGoalAsync();
            var request = new CreateStepRequest("Step without note");

            var result = await _stepService.CreateAsync(goal.Id, request, _userId);

            result.Notes.Should().BeNull();
        }

        // ── Application: UpdateNotesAsync ────────────────────────────────────────

        [Fact]
        public async Task UpdateNotesAsync_AddsNotes()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Notes: "New note"), _userId);

            updated.Notes.Should().Be("New note");
        }

        [Fact]
        public async Task UpdateNotesAsync_WithClearNotes_RemovesNotes()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", "Original note"), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(ClearNotes: true), _userId);

            updated.Notes.Should().BeNull();
        }

        [Fact]
        public async Task UpdateNotesAsync_WithNullNotes_KeepsExistingNotes()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", "Keep me"), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(), _userId);

            updated.Notes.Should().Be("Keep me");
        }

        [Fact]
        public async Task UpdateNotesAsync_WrongUser_ThrowsUnauthorized()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step"), _userId);
            var otherUser = Guid.NewGuid();

            var act = async () => await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Notes: "Note"), otherUser);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task UpdateNotesAsync_StepNotFound_ThrowsArgumentException()
        {
            var goal = await CreateGoalAsync();

            var act = async () => await _stepService.UpdateAsync(goal.Id, Guid.NewGuid(), new UpdateStepRequest(Notes: "Note"), _userId);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        // ── Application: Notes preserved through list operations ─────────────────

        [Fact]
        public async Task ListByGoalAsync_StepWithNotes_ReturnsNotesInResponse()
        {
            var goal = await CreateGoalAsync();
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step with note", "Listed note"), _userId);

            var steps = await _stepService.ListByGoalAsync(goal.Id, _userId);

            steps.Should().ContainSingle();
            steps[0].Notes.Should().Be("Listed note");
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_StepWithNotes_ReturnsNotesInResponse()
        {
            var goal = await CreateGoalAsync();
            await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", "Filter note"), _userId);

            var filter = new StepFilterRequest();
            var result = await _stepService.ListByGoalFilteredAsync(goal.Id, _userId, filter);

            result.Items.Should().ContainSingle();
            result.Items[0].Notes.Should().Be("Filter note");
        }

        [Fact]
        public async Task MarkCompletedAsync_PreservesNotes()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Step", "Preserved note"), _userId);

            var completed = await _stepService.MarkCompletedAsync(goal.Id, created.Id, _userId);

            completed.Notes.Should().Be("Preserved note");
            completed.IsCompleted.Should().BeTrue();
        }
    }
}
