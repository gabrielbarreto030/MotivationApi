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
    public class MotivationUpdateTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly MotivationService _motivationService;

        public MotivationUpdateTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_MotivationUpdate_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _motivationRepository = new MotivationRepository(_context, _cache);
            _motivationService = new MotivationService(_motivationRepository, _goalRepository, NullLogger<MotivationService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> CreateGoalAsync(Guid userId, string title = "Goal")
        {
            var goal = new Goal(Guid.NewGuid(), userId, title, "Description", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        [Fact]
        public async Task UpdateAsync_ValidRequest_UpdatesText()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var added = await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Original text"), userId);

            var result = await _motivationService.UpdateAsync(goal.Id, added.Id, new UpdateMotivationRequest("Updated text"), userId);

            result.Text.Should().Be("Updated text");
            result.Id.Should().Be(added.Id);
            result.GoalId.Should().Be(goal.Id);
        }

        [Fact]
        public async Task UpdateAsync_PersistsChange_VisibleInSubsequentList()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var added = await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Before"), userId);

            await _motivationService.UpdateAsync(goal.Id, added.Id, new UpdateMotivationRequest("After"), userId);

            var list = await _motivationService.ListByGoalAsync(goal.Id, userId);
            list.Should().Contain(m => m.Id == added.Id && m.Text == "After");
        }

        [Fact]
        public async Task UpdateAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();

            Func<Task> act = async () =>
                await _motivationService.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateMotivationRequest("Text"), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task UpdateAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var goal = await CreateGoalAsync(ownerId);
            var added = await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Text"), ownerId);

            Func<Task> act = async () =>
                await _motivationService.UpdateAsync(goal.Id, added.Id, new UpdateMotivationRequest("Hacked"), otherId);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task UpdateAsync_MotivationNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            Func<Task> act = async () =>
                await _motivationService.UpdateAsync(goal.Id, Guid.NewGuid(), new UpdateMotivationRequest("Text"), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Motivation not found*");
        }

        [Fact]
        public async Task UpdateAsync_MotivationBelongsToDifferentGoal_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal1 = await CreateGoalAsync(userId, "Goal 1");
            var goal2 = await CreateGoalAsync(userId, "Goal 2");
            var added = await _motivationService.AddAsync(goal1.Id, new AddMotivationRequest("For goal 1"), userId);

            Func<Task> act = async () =>
                await _motivationService.UpdateAsync(goal2.Id, added.Id, new UpdateMotivationRequest("Wrong goal"), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not belong to this goal*");
        }

        [Fact]
        public async Task UpdateAsync_EmptyText_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var added = await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Original"), userId);

            Func<Task> act = async () =>
                await _motivationService.UpdateAsync(goal.Id, added.Id, new UpdateMotivationRequest("   "), userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Text is required*");
        }

        [Fact]
        public void UpdateText_EmptyText_ThrowsArgumentException()
        {
            var motivation = new Domain.Entities.Motivation(Guid.NewGuid(), Guid.NewGuid(), "Original");

            Action act = () => motivation.UpdateText("");

            act.Should().Throw<ArgumentException>().WithMessage("*Text is required*");
        }

        [Fact]
        public void UpdateText_ValidText_ChangesText()
        {
            var motivation = new Domain.Entities.Motivation(Guid.NewGuid(), Guid.NewGuid(), "Original");

            motivation.UpdateText("New text");

            motivation.Text.Should().Be("New text");
        }
    }
}
