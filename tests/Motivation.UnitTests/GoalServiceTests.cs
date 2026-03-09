using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Motivation.Application.DTOs;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Domain.Interfaces;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class GoalServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly GoalService _goalService;

        public GoalServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Goal_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _goalService = new GoalService(_goalRepository);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_CreatesGoal()
        {
            var userId = Guid.NewGuid();
            var request = new CreateGoalRequest("Test Goal", "Test Description");

            var result = await _goalService.CreateAsync(request, userId);

            result.Id.Should().NotBeEmpty();
            result.Title.Should().Be("Test Goal");
            result.Description.Should().Be("Test Description");
            result.Status.Should().Be(GoalStatus.Pending);
            result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

            var stored = await _goalRepository.GetByUserAsync(userId);
            stored.Should().HaveCount(1);
            stored[0].Title.Should().Be("Test Goal");
        }

        [Fact]
        public async Task CreateAsync_EmptyTitle_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var request = new CreateGoalRequest("", "Description");

            Func<Task> act = async () => await _goalService.CreateAsync(request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Title*");
        }

        [Fact]
        public async Task CreateAsync_EmptyDescription_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var request = new CreateGoalRequest("Title", "");

            Func<Task> act = async () => await _goalService.CreateAsync(request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Description*");
        }

        [Fact]
        public async Task ListByUserAsync_ReturnsGoals()
        {
            var userId = Guid.NewGuid();
            // create two goals via repository directly
            var g1 = new Goal(Guid.NewGuid(), userId, "A", "desc", GoalStatus.Pending, DateTime.UtcNow);
            var g2 = new Goal(Guid.NewGuid(), userId, "B", "desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(g1);
            await _goalRepository.AddAsync(g2);

            var results = await _goalService.ListByUserAsync(userId);
            results.Should().HaveCount(2);
            results.Select(r => r.Title).Should().Contain(new[] {"A","B"});
        }

        [Fact]
        public async Task UpdateAsync_ValidRequest_UpdatesGoal()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Original Title", "Original Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var request = new UpdateGoalRequest("Updated Title", "Updated Description", "InProgress");
            var result = await _goalService.UpdateAsync(goal.Id, request, userId);

            result.Title.Should().Be("Updated Title");
            result.Description.Should().Be("Updated Description");
            result.Status.Should().Be(GoalStatus.InProgress);

            // Verify it was persisted
            var updated = await _goalRepository.GetByIdAsync(goal.Id);
            updated.Should().NotBeNull();
            updated!.Title.Should().Be("Updated Title");
            updated.Description.Should().Be("Updated Description");
        }

        [Fact]
        public async Task UpdateAsync_PartialUpdate_UpdatesOnlyProvidedFields()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Original Title", "Original Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            // Only update title
            var request = new UpdateGoalRequest("New Title", null, null);
            var result = await _goalService.UpdateAsync(goal.Id, request, userId);

            result.Title.Should().Be("New Title");
            result.Description.Should().Be("Original Description");
            result.Status.Should().Be(GoalStatus.Pending);
        }

        [Fact]
        public async Task UpdateAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var request = new UpdateGoalRequest("Title", "Description", null);

            Func<Task> act = async () => await _goalService.UpdateAsync(Guid.NewGuid(), request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task UpdateAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Title", "Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var request = new UpdateGoalRequest("New Title", null, null);

            Func<Task> act = async () => await _goalService.UpdateAsync(goal.Id, request, otherUserId);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task UpdateAsync_InvalidStatus_IgnoresStatus()
        {
            var userId = Guid.NewGuid();
            var goal = new Goal(Guid.NewGuid(), userId, "Title", "Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            var request = new UpdateGoalRequest(null, null, "InvalidStatus");
            var result = await _goalService.UpdateAsync(goal.Id, request, userId);

            result.Status.Should().Be(GoalStatus.Pending); // Should remain unchanged
        }
    }
}