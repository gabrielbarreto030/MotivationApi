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
    }
}