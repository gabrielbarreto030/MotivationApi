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
    public class MotivationStatsTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly MotivationService _motivationService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _goalId;

        public MotivationStatsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_MotivationStats_" + Guid.NewGuid())
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

        private async Task<AddMotivationResponse> AddAsync(string text = "Keep going", string[]? tags = null)
        {
            var req = new AddMotivationRequest(text, tags);
            return await _motivationService.AddAsync(_goalId, req, _userId);
        }

        // ── Empty goal ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_NoMotivations_ReturnsZeroStats()
        {
            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.TotalMotivations.Should().Be(0);
            stats.TotalFavorites.Should().Be(0);
            stats.RatedMotivations.Should().Be(0);
            stats.AverageRating.Should().BeNull();
            stats.TagBreakdown.Should().BeEmpty();
        }

        // ── TotalMotivations ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_ThreeMotivations_TotalIsThree()
        {
            await AddAsync("A");
            await AddAsync("B");
            await AddAsync("C");

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.TotalMotivations.Should().Be(3);
        }

        // ── TotalFavorites ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_TwoFavorites_CountsCorrectly()
        {
            var m1 = await AddAsync("A");
            var m2 = await AddAsync("B");
            await AddAsync("C");

            await _motivationService.FavoriteAsync(_goalId, m1.Id, _userId);
            await _motivationService.FavoriteAsync(_goalId, m2.Id, _userId);

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.TotalFavorites.Should().Be(2);
        }

        [Fact]
        public async Task GetStatsAsync_NoFavorites_TotalFavoritesIsZero()
        {
            await AddAsync("A");
            await AddAsync("B");

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.TotalFavorites.Should().Be(0);
        }

        // ── RatedMotivations / AverageRating ─────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_AllRated_AverageRatingIsCorrect()
        {
            var m1 = await AddAsync("A");
            var m2 = await AddAsync("B");
            var m3 = await AddAsync("C");

            await _motivationService.RateAsync(_goalId, m1.Id, new RateMotivationRequest(2), _userId);
            await _motivationService.RateAsync(_goalId, m2.Id, new RateMotivationRequest(4), _userId);
            await _motivationService.RateAsync(_goalId, m3.Id, new RateMotivationRequest(3), _userId);

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.RatedMotivations.Should().Be(3);
            stats.AverageRating.Should().BeApproximately(3.0, 0.001);
        }

        [Fact]
        public async Task GetStatsAsync_SomeRated_OnlyCountsRated()
        {
            var m1 = await AddAsync("A");
            await AddAsync("B"); // not rated

            await _motivationService.RateAsync(_goalId, m1.Id, new RateMotivationRequest(5), _userId);

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.RatedMotivations.Should().Be(1);
            stats.AverageRating.Should().BeApproximately(5.0, 0.001);
        }

        [Fact]
        public async Task GetStatsAsync_NoneRated_AverageRatingIsNull()
        {
            await AddAsync("A");
            await AddAsync("B");

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.RatedMotivations.Should().Be(0);
            stats.AverageRating.Should().BeNull();
        }

        // ── TagBreakdown ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_WithTags_TagBreakdownIsCorrect()
        {
            await AddAsync("A", new[] { "focus", "energy" });
            await AddAsync("B", new[] { "focus" });
            await AddAsync("C", new[] { "calm" });

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.TagBreakdown.Should().ContainKey("focus").WhoseValue.Should().Be(2);
            stats.TagBreakdown.Should().ContainKey("energy").WhoseValue.Should().Be(1);
            stats.TagBreakdown.Should().ContainKey("calm").WhoseValue.Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_NoTags_TagBreakdownIsEmpty()
        {
            await AddAsync("A");
            await AddAsync("B");

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.TagBreakdown.Should().BeEmpty();
        }

        [Fact]
        public async Task GetStatsAsync_MixedTagsAndNoTags_OnlyTaggedCounted()
        {
            await AddAsync("A", new[] { "mindset" });
            await AddAsync("B"); // no tags

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.TagBreakdown.Should().HaveCount(1);
            stats.TagBreakdown["mindset"].Should().Be(1);
        }

        // ── Authorization ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_GoalNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _motivationService.GetStatsAsync(Guid.NewGuid(), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task GetStatsAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            Func<Task> act = async () => await _motivationService.GetStatsAsync(_goalId, Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        // ── Combined scenario ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatsAsync_FullScenario_AllFieldsCorrect()
        {
            var m1 = await AddAsync("Believe", new[] { "motivation" });
            var m2 = await AddAsync("Focus",   new[] { "motivation", "focus" });
            var m3 = await AddAsync("Rest",    new[] { "balance" });
            var m4 = await AddAsync("Go!");    // no tags

            await _motivationService.FavoriteAsync(_goalId, m1.Id, _userId);
            await _motivationService.FavoriteAsync(_goalId, m3.Id, _userId);

            await _motivationService.RateAsync(_goalId, m1.Id, new RateMotivationRequest(5), _userId);
            await _motivationService.RateAsync(_goalId, m2.Id, new RateMotivationRequest(3), _userId);

            var stats = await _motivationService.GetStatsAsync(_goalId, _userId);

            stats.TotalMotivations.Should().Be(4);
            stats.TotalFavorites.Should().Be(2);
            stats.RatedMotivations.Should().Be(2);
            stats.AverageRating.Should().BeApproximately(4.0, 0.001);
            stats.TagBreakdown["motivation"].Should().Be(2);
            stats.TagBreakdown["focus"].Should().Be(1);
            stats.TagBreakdown["balance"].Should().Be(1);
            stats.TagBreakdown.Should().NotContainKey("Go!");
        }
    }
}
