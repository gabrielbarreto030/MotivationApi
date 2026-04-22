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
    public class StepTitleUpdateTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly StepService _stepService;
        private readonly Guid _userId = Guid.NewGuid();

        public StepTitleUpdateTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_StepTitleUpdate_" + Guid.NewGuid())
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

        // ── Domain: UpdateTitle ──────────────────────────────────────────────────

        [Fact]
        public void Step_UpdateTitle_ChangesTitle()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Original Title");

            step.UpdateTitle("New Title");

            step.Title.Should().Be("New Title");
        }

        [Fact]
        public void Step_UpdateTitle_WithNullTitle_ThrowsArgumentException()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");

            Action act = () => step.UpdateTitle(null!);

            act.Should().Throw<ArgumentException>().WithParameterName("title");
        }

        [Fact]
        public void Step_UpdateTitle_WithEmptyTitle_ThrowsArgumentException()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");

            Action act = () => step.UpdateTitle(string.Empty);

            act.Should().Throw<ArgumentException>().WithParameterName("title");
        }

        [Fact]
        public void Step_UpdateTitle_WithWhitespace_ThrowsArgumentException()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Title");

            Action act = () => step.UpdateTitle("   ");

            act.Should().Throw<ArgumentException>().WithParameterName("title");
        }

        [Fact]
        public void Step_UpdateTitle_DoesNotChangeOtherProperties()
        {
            var id = Guid.NewGuid();
            var goalId = Guid.NewGuid();
            var step = new Step(id, goalId, "Original", "Some notes");

            step.UpdateTitle("Updated Title");

            step.Id.Should().Be(id);
            step.GoalId.Should().Be(goalId);
            step.Notes.Should().Be("Some notes");
            step.IsCompleted.Should().BeFalse();
        }

        // ── Application: UpdateAsync with Title ──────────────────────────────────

        [Fact]
        public async Task UpdateAsync_WithTitle_UpdatesTitle()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Original Title"), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Title: "New Title"), _userId);

            updated.Title.Should().Be("New Title");
        }

        [Fact]
        public async Task UpdateAsync_WithTitle_PreservesNotes()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Title", "Keep note"), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Title: "New Title"), _userId);

            updated.Title.Should().Be("New Title");
            updated.Notes.Should().Be("Keep note");
        }

        [Fact]
        public async Task UpdateAsync_WithTitleAndNotes_UpdatesBoth()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Old Title"), _userId);

            var updated = await _stepService.UpdateAsync(
                goal.Id, created.Id,
                new UpdateStepRequest(Title: "New Title", Notes: "New Notes"),
                _userId);

            updated.Title.Should().Be("New Title");
            updated.Notes.Should().Be("New Notes");
        }

        [Fact]
        public async Task UpdateAsync_WithNullTitle_KeepsExistingTitle()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Keep Title"), _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Notes: "Note"), _userId);

            updated.Title.Should().Be("Keep Title");
        }

        [Fact]
        public async Task UpdateAsync_WithTitle_PreservesCompletionState()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Title"), _userId);
            await _stepService.MarkCompletedAsync(goal.Id, created.Id, _userId);

            var updated = await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Title: "Updated Title"), _userId);

            updated.Title.Should().Be("Updated Title");
            updated.IsCompleted.Should().BeTrue();
            updated.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateAsync_WrongUser_ThrowsUnauthorized()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Title"), _userId);
            var otherUser = Guid.NewGuid();

            var act = async () => await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Title: "New"), otherUser);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task UpdateAsync_StepNotFound_ThrowsArgumentException()
        {
            var goal = await CreateGoalAsync();

            var act = async () => await _stepService.UpdateAsync(goal.Id, Guid.NewGuid(), new UpdateStepRequest(Title: "New"), _userId);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task UpdateAsync_TitleAndClearNotes_UpdatesTitleAndClearsNotes()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Old Title", "Remove me"), _userId);

            var updated = await _stepService.UpdateAsync(
                goal.Id, created.Id,
                new UpdateStepRequest(Title: "New Title", ClearNotes: true),
                _userId);

            updated.Title.Should().Be("New Title");
            updated.Notes.Should().BeNull();
        }

        // ── Application: ListByGoal reflects updated title ────────────────────────

        [Fact]
        public async Task ListByGoalAsync_ReflectsUpdatedTitle()
        {
            var goal = await CreateGoalAsync();
            var created = await _stepService.CreateAsync(goal.Id, new CreateStepRequest("Old Title"), _userId);
            await _stepService.UpdateAsync(goal.Id, created.Id, new UpdateStepRequest(Title: "Updated Title"), _userId);

            var steps = await _stepService.ListByGoalAsync(goal.Id, _userId);

            steps.Should().ContainSingle();
            steps[0].Title.Should().Be("Updated Title");
        }
    }
}
