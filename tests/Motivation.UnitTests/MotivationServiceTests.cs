using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Motivation.Application.DTOs;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class MotivationServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly MotivationService _motivationService;

        public MotivationServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Motivation_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _motivationRepository = new MotivationRepository(_context, _cache);
            _motivationService = new MotivationService(_motivationRepository, _goalRepository);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> CreateGoalAsync(Guid userId, string title = "Goal", string description = "Desc")
        {
            var goal = new Goal(Guid.NewGuid(), userId, title, description, GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        [Fact]
        public async Task AddAsync_ValidRequest_CreatesMotivation()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var request = new AddMotivationRequest("Keep going!");
            var result = await _motivationService.AddAsync(goal.Id, request, userId);

            result.Id.Should().NotBeEmpty();
            result.GoalId.Should().Be(goal.Id);
            result.Text.Should().Be("Keep going!");
        }

        [Fact]
        public async Task AddAsync_EmptyText_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var request = new AddMotivationRequest("");

            Func<Task> act = async () => await _motivationService.AddAsync(goal.Id, request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Text*");
        }

        [Fact]
        public async Task AddAsync_WhitespaceText_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var request = new AddMotivationRequest("   ");

            Func<Task> act = async () => await _motivationService.AddAsync(goal.Id, request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Text*");
        }

        [Fact]
        public async Task AddAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var request = new AddMotivationRequest("Keep going!");

            Func<Task> act = async () => await _motivationService.AddAsync(Guid.NewGuid(), request, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task AddAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var goal = await CreateGoalAsync(ownerId);

            var request = new AddMotivationRequest("You can do it!");

            Func<Task> act = async () => await _motivationService.AddAsync(goal.Id, request, otherId);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task AddAsync_PersistsMotivation_InRepository()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            var request = new AddMotivationRequest("Stay focused!");
            var result = await _motivationService.AddAsync(goal.Id, request, userId);

            var motivations = await _motivationRepository.GetByGoalAsync(goal.Id);
            motivations.Should().HaveCount(1);
            motivations[0].Id.Should().Be(result.Id);
            motivations[0].Text.Should().Be("Stay focused!");
            motivations[0].GoalId.Should().Be(goal.Id);
        }

        [Fact]
        public async Task AddAsync_MultipleMotivations_AllPersisted()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Motivation 1"), userId);
            await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Motivation 2"), userId);
            await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Motivation 3"), userId);

            var motivations = await _motivationRepository.GetByGoalAsync(goal.Id);
            motivations.Should().HaveCount(3);
        }

        [Fact]
        public async Task AddAsync_MotivationsIsolatedPerGoal()
        {
            var userId = Guid.NewGuid();
            var goal1 = await CreateGoalAsync(userId, "Goal 1");
            var goal2 = await CreateGoalAsync(userId, "Goal 2");

            await _motivationService.AddAsync(goal1.Id, new AddMotivationRequest("For goal 1"), userId);
            await _motivationService.AddAsync(goal2.Id, new AddMotivationRequest("For goal 2a"), userId);
            await _motivationService.AddAsync(goal2.Id, new AddMotivationRequest("For goal 2b"), userId);

            var goal1Motivations = await _motivationRepository.GetByGoalAsync(goal1.Id);
            var goal2Motivations = await _motivationRepository.GetByGoalAsync(goal2.Id);

            goal1Motivations.Should().HaveCount(1);
            goal2Motivations.Should().HaveCount(2);
        }

        [Fact]
        public async Task RemoveAsync_ValidRequest_DeletesMotivation()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var added = await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Keep going!"), userId);

            await _motivationService.RemoveAsync(goal.Id, added.Id, userId);

            var motivations = await _motivationRepository.GetByGoalAsync(goal.Id);
            motivations.Should().BeEmpty();
        }

        [Fact]
        public async Task RemoveAsync_GoalNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();

            Func<Task> act = async () => await _motivationService.RemoveAsync(Guid.NewGuid(), Guid.NewGuid(), userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task RemoveAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var goal = await CreateGoalAsync(ownerId);
            var added = await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Mine!"), ownerId);

            Func<Task> act = async () => await _motivationService.RemoveAsync(goal.Id, added.Id, otherId);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task RemoveAsync_MotivationNotFound_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);

            Func<Task> act = async () => await _motivationService.RemoveAsync(goal.Id, Guid.NewGuid(), userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Motivation not found*");
        }

        [Fact]
        public async Task RemoveAsync_MotivationBelongsToDifferentGoal_ThrowsArgumentException()
        {
            var userId = Guid.NewGuid();
            var goal1 = await CreateGoalAsync(userId, "Goal 1");
            var goal2 = await CreateGoalAsync(userId, "Goal 2");
            var added = await _motivationService.AddAsync(goal1.Id, new AddMotivationRequest("For goal 1"), userId);

            Func<Task> act = async () => await _motivationService.RemoveAsync(goal2.Id, added.Id, userId);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not belong*");
        }

        [Fact]
        public async Task RemoveAsync_RemainingMotivations_StillExist()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            var first = await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("First"), userId);
            var second = await _motivationService.AddAsync(goal.Id, new AddMotivationRequest("Second"), userId);

            await _motivationService.RemoveAsync(goal.Id, first.Id, userId);

            var motivations = await _motivationRepository.GetByGoalAsync(goal.Id);
            motivations.Should().HaveCount(1);
            motivations[0].Id.Should().Be(second.Id);
        }
    }
}
