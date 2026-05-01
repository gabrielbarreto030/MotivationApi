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
    public class GoalArchivingTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly StepRepository _stepRepository;
        private readonly GoalService _goalService;
        private readonly Guid _userId = Guid.NewGuid();

        public GoalArchivingTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_GoalArchiving_" + Guid.NewGuid())
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

        private async Task<Goal> SeedGoalAsync(string title = "Goal")
        {
            var goal = new Goal(Guid.NewGuid(), _userId, title, "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        // ── Domain: Archive/Unarchive ─────────────────────────────────────────────

        [Fact]
        public void Goal_DefaultIsArchived_IsFalse()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.IsArchived.Should().BeFalse();
        }

        [Fact]
        public void Goal_Archive_SetsIsArchivedTrue()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.Archive();

            goal.IsArchived.Should().BeTrue();
        }

        [Fact]
        public void Goal_Unarchive_SetsIsArchivedFalse()
        {
            var goal = new Goal(Guid.NewGuid(), _userId, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            goal.Archive();

            goal.Unarchive();

            goal.IsArchived.Should().BeFalse();
        }

        // ── Application: ArchiveAsync ─────────────────────────────────────────────

        [Fact]
        public async Task ArchiveAsync_ValidGoal_ReturnsIsArchivedTrue()
        {
            var goal = await SeedGoalAsync();

            var result = await _goalService.ArchiveAsync(goal.Id, _userId);

            result.IsArchived.Should().BeTrue();
        }

        [Fact]
        public async Task ArchiveAsync_PersistsToRepository()
        {
            var goal = await SeedGoalAsync();

            await _goalService.ArchiveAsync(goal.Id, _userId);

            var stored = await _goalRepository.GetByIdAsync(goal.Id);
            stored!.IsArchived.Should().BeTrue();
        }

        [Fact]
        public async Task ArchiveAsync_GoalNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _goalService.ArchiveAsync(Guid.NewGuid(), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task ArchiveAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            var goal = await SeedGoalAsync();

            Func<Task> act = async () => await _goalService.ArchiveAsync(goal.Id, Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        // ── Application: UnarchiveAsync ───────────────────────────────────────────

        [Fact]
        public async Task UnarchiveAsync_ArchivedGoal_ReturnsIsArchivedFalse()
        {
            var goal = await SeedGoalAsync();
            await _goalService.ArchiveAsync(goal.Id, _userId);

            var result = await _goalService.UnarchiveAsync(goal.Id, _userId);

            result.IsArchived.Should().BeFalse();
        }

        [Fact]
        public async Task UnarchiveAsync_GoalNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _goalService.UnarchiveAsync(Guid.NewGuid(), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task UnarchiveAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            var goal = await SeedGoalAsync();
            await _goalService.ArchiveAsync(goal.Id, _userId);

            Func<Task> act = async () => await _goalService.UnarchiveAsync(goal.Id, Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        // ── Application: GetArchivedAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetArchivedAsync_NoArchivedGoals_ReturnsEmpty()
        {
            await SeedGoalAsync("Active Goal");

            var result = await _goalService.GetArchivedAsync(_userId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetArchivedAsync_ReturnsOnlyArchivedGoals()
        {
            var active = await SeedGoalAsync("Active");
            var archived = await SeedGoalAsync("Archived");
            await _goalService.ArchiveAsync(archived.Id, _userId);

            var result = await _goalService.GetArchivedAsync(_userId);

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(archived.Id);
            result[0].IsArchived.Should().BeTrue();
        }

        // ── Application: ListByUserFilteredAsync excludes archived by default ─────

        [Fact]
        public async Task ListByUserFilteredAsync_Default_ExcludesArchivedGoals()
        {
            await SeedGoalAsync("Active");
            var archived = await SeedGoalAsync("Archived");
            await _goalService.ArchiveAsync(archived.Id, _userId);

            var filter = new GoalFilterRequest();
            var result = await _goalService.ListByUserFilteredAsync(_userId, filter);

            result.Items.Should().OnlyContain(g => !g.IsArchived);
            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task ListByUserFilteredAsync_IncludeArchived_ReturnsAll()
        {
            await SeedGoalAsync("Active");
            var archived = await SeedGoalAsync("Archived");
            await _goalService.ArchiveAsync(archived.Id, _userId);

            var filter = new GoalFilterRequest(includeArchived: true);
            var result = await _goalService.ListByUserFilteredAsync(_userId, filter);

            result.Items.Should().HaveCount(2);
        }

        // ── Application: CreateGoalResponse includes IsArchived ───────────────────

        [Fact]
        public async Task CreateAsync_NewGoal_IsArchivedFalseInResponse()
        {
            var result = await _goalService.CreateAsync(new CreateGoalRequest("Title", "Desc"), _userId);

            result.IsArchived.Should().BeFalse();
        }
    }
}
