using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Motivation.Application.DTOs;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class DailyMessageServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly DailyMessageService _dailyMessageService;

        public DailyMessageServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_DailyMessage_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _goalRepository = new GoalRepository(_context, _cache);
            _motivationRepository = new MotivationRepository(_context, _cache);
            _dailyMessageService = new DailyMessageService(_goalRepository, _motivationRepository, NullLogger<DailyMessageService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<Goal> CreateGoalAsync(Guid userId, string title = "Goal")
        {
            var goal = new Goal(Guid.NewGuid(), userId, title, "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);
            return goal;
        }

        private async Task AddMotivationAsync(Guid goalId, string text)
        {
            var motivation = new Domain.Entities.Motivation(Guid.NewGuid(), goalId, text);
            await _motivationRepository.AddAsync(motivation);
        }

        [Fact]
        public async Task GetDailyMessageAsync_NoMotivations_ReturnsDefaultMessage()
        {
            var userId = Guid.NewGuid();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await _dailyMessageService.GetDailyMessageAsync(userId, today);

            result.Message.Should().NotBeNullOrWhiteSpace();
            result.Message.Should().Be("Keep going! Every step forward is progress.");
            result.Date.Should().Be(today);
        }

        [Fact]
        public async Task GetDailyMessageAsync_WithMotivations_ReturnsOneOfThem()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            await AddMotivationAsync(goal.Id, "You can do it!");
            await AddMotivationAsync(goal.Id, "Stay focused!");
            await AddMotivationAsync(goal.Id, "Never give up!");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await _dailyMessageService.GetDailyMessageAsync(userId, today);

            result.Message.Should().BeOneOf("You can do it!", "Stay focused!", "Never give up!");
            result.Date.Should().Be(today);
        }

        [Fact]
        public async Task GetDailyMessageAsync_SameDateAlwaysReturnsSameMessage()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            await AddMotivationAsync(goal.Id, "Message A");
            await AddMotivationAsync(goal.Id, "Message B");
            await AddMotivationAsync(goal.Id, "Message C");

            var fixedDate = new DateOnly(2026, 6, 15);

            var result1 = await _dailyMessageService.GetDailyMessageAsync(userId, fixedDate);
            var result2 = await _dailyMessageService.GetDailyMessageAsync(userId, fixedDate);

            result1.Message.Should().Be(result2.Message);
        }

        [Fact]
        public async Task GetDailyMessageAsync_AggregatesMotivationsAcrossGoals()
        {
            var userId = Guid.NewGuid();
            var goal1 = await CreateGoalAsync(userId, "Goal 1");
            var goal2 = await CreateGoalAsync(userId, "Goal 2");

            await AddMotivationAsync(goal1.Id, "From goal 1");
            await AddMotivationAsync(goal2.Id, "From goal 2");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await _dailyMessageService.GetDailyMessageAsync(userId, today);

            result.Message.Should().BeOneOf("From goal 1", "From goal 2");
        }

        [Fact]
        public async Task GetDailyMessageAsync_UsesUtcDateWhenNotProvided()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            await AddMotivationAsync(goal.Id, "Today's message");

            var result = await _dailyMessageService.GetDailyMessageAsync(userId);

            result.Date.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
            result.Message.Should().Be("Today's message");
        }

        [Fact]
        public async Task GetDailyMessageAsync_IsolatedPerUser_DoesNotMixMotivations()
        {
            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();

            var goal1 = await CreateGoalAsync(user1, "User1 Goal");
            var goal2 = await CreateGoalAsync(user2, "User2 Goal");

            await AddMotivationAsync(goal1.Id, "User1 motivation");
            await AddMotivationAsync(goal2.Id, "User2 motivation");

            var today = new DateOnly(2026, 1, 1);

            var result1 = await _dailyMessageService.GetDailyMessageAsync(user1, today);
            var result2 = await _dailyMessageService.GetDailyMessageAsync(user2, today);

            result1.Message.Should().Be("User1 motivation");
            result2.Message.Should().Be("User2 motivation");
        }

        [Fact]
        public async Task GetDailyMessageAsync_SingleMotivation_AlwaysReturnsThatMessage()
        {
            var userId = Guid.NewGuid();
            var goal = await CreateGoalAsync(userId);
            await AddMotivationAsync(goal.Id, "The only message");

            var date1 = new DateOnly(2026, 1, 1);
            var date2 = new DateOnly(2026, 6, 15);
            var date3 = new DateOnly(2026, 12, 31);

            var r1 = await _dailyMessageService.GetDailyMessageAsync(userId, date1);
            var r2 = await _dailyMessageService.GetDailyMessageAsync(userId, date2);
            var r3 = await _dailyMessageService.GetDailyMessageAsync(userId, date3);

            r1.Message.Should().Be("The only message");
            r2.Message.Should().Be("The only message");
            r3.Message.Should().Be("The only message");
        }
    }
}
