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
    public class MotivationRatingTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly MotivationService _motivationService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _goalId;

        public MotivationRatingTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_MotivationRating_" + Guid.NewGuid())
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

        private async Task<AddMotivationResponse> AddMotivationAsync(string text = "Keep going")
            => await _motivationService.AddAsync(_goalId, new AddMotivationRequest(text), _userId);

        // ── Domain: Rate / ClearRating ────────────────────────────────────────────

        [Fact]
        public void Motivation_DefaultRating_IsNull()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            m.Rating.Should().BeNull();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void Motivation_Rate_ValidValue_SetsRating(int rating)
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            m.Rate(rating);

            m.Rating.Should().Be(rating);
        }

        [Fact]
        public void Motivation_Rate_CanOverwriteExistingRating()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");
            m.Rate(3);

            m.Rate(5);

            m.Rating.Should().Be(5);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        [InlineData(-1)]
        public void Motivation_Rate_OutOfRange_ThrowsArgumentOutOfRangeException(int rating)
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            Action act = () => m.Rate(rating);

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Rating must be between 1 and 5*");
        }

        [Fact]
        public void Motivation_ClearRating_SetsRatingNull()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");
            m.Rate(4);

            m.ClearRating();

            m.Rating.Should().BeNull();
        }

        [Fact]
        public void Motivation_ClearRating_WhenAlreadyNull_DoesNotThrow()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            Action act = () => m.ClearRating();

            act.Should().NotThrow();
            m.Rating.Should().BeNull();
        }

        [Fact]
        public void Motivation_Constructor_WithRating_PreservesValue()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text", rating: 3);

            m.Rating.Should().Be(3);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        public void Motivation_Constructor_InvalidRating_ThrowsArgumentOutOfRangeException(int rating)
        {
            Action act = () => new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text", rating: rating);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        // ── Application: RateAsync ────────────────────────────────────────────────

        [Fact]
        public async Task RateAsync_ValidRating_ReturnsUpdatedRating()
        {
            var added = await AddMotivationAsync();

            var result = await _motivationService.RateAsync(_goalId, added.Id, new RateMotivationRequest(4), _userId);

            result.Rating.Should().Be(4);
        }

        [Fact]
        public async Task RateAsync_PersistsToRepository()
        {
            var added = await AddMotivationAsync();

            await _motivationService.RateAsync(_goalId, added.Id, new RateMotivationRequest(5), _userId);

            var stored = await _motivationRepository.GetByIdAsync(added.Id);
            stored!.Rating.Should().Be(5);
        }

        [Fact]
        public async Task RateAsync_CanUpdateRating()
        {
            var added = await AddMotivationAsync();
            await _motivationService.RateAsync(_goalId, added.Id, new RateMotivationRequest(2), _userId);

            var result = await _motivationService.RateAsync(_goalId, added.Id, new RateMotivationRequest(5), _userId);

            result.Rating.Should().Be(5);
        }

        [Fact]
        public async Task RateAsync_GoalNotFound_ThrowsArgumentException()
        {
            var added = await AddMotivationAsync();

            Func<Task> act = async () => await _motivationService.RateAsync(Guid.NewGuid(), added.Id, new RateMotivationRequest(3), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task RateAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            var added = await AddMotivationAsync();

            Func<Task> act = async () => await _motivationService.RateAsync(_goalId, added.Id, new RateMotivationRequest(3), Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task RateAsync_MotivationNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _motivationService.RateAsync(_goalId, Guid.NewGuid(), new RateMotivationRequest(3), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Motivation not found*");
        }

        [Fact]
        public async Task RateAsync_InvalidRating_ThrowsArgumentOutOfRangeException()
        {
            var added = await AddMotivationAsync();

            Func<Task> act = async () => await _motivationService.RateAsync(_goalId, added.Id, new RateMotivationRequest(6), _userId);

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        // ── Application: ClearRatingAsync ─────────────────────────────────────────

        [Fact]
        public async Task ClearRatingAsync_RatedMotivation_ReturnsNullRating()
        {
            var added = await AddMotivationAsync();
            await _motivationService.RateAsync(_goalId, added.Id, new RateMotivationRequest(3), _userId);

            var result = await _motivationService.ClearRatingAsync(_goalId, added.Id, _userId);

            result.Rating.Should().BeNull();
        }

        [Fact]
        public async Task ClearRatingAsync_PersistsToRepository()
        {
            var added = await AddMotivationAsync();
            await _motivationService.RateAsync(_goalId, added.Id, new RateMotivationRequest(4), _userId);

            await _motivationService.ClearRatingAsync(_goalId, added.Id, _userId);

            var stored = await _motivationRepository.GetByIdAsync(added.Id);
            stored!.Rating.Should().BeNull();
        }

        [Fact]
        public async Task ClearRatingAsync_GoalNotFound_ThrowsArgumentException()
        {
            var added = await AddMotivationAsync();

            Func<Task> act = async () => await _motivationService.ClearRatingAsync(Guid.NewGuid(), added.Id, _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task ClearRatingAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            var added = await AddMotivationAsync();

            Func<Task> act = async () => await _motivationService.ClearRatingAsync(_goalId, added.Id, Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        // ── Application: AddAsync returns Rating=null ─────────────────────────────

        [Fact]
        public async Task AddAsync_NewMotivation_RatingIsNullInResponse()
        {
            var result = await AddMotivationAsync();

            result.Rating.Should().BeNull();
        }

        // ── Application: ListByGoalFilteredAsync with MinRating ───────────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_MinRating_ReturnsOnlyAboveOrEqual()
        {
            var m1 = await AddMotivationAsync("Phrase 1");
            var m2 = await AddMotivationAsync("Phrase 2");
            var m3 = await AddMotivationAsync("Phrase 3");
            var m4 = await AddMotivationAsync("Phrase 4");

            await _motivationService.RateAsync(_goalId, m1.Id, new RateMotivationRequest(5), _userId);
            await _motivationService.RateAsync(_goalId, m2.Id, new RateMotivationRequest(3), _userId);
            await _motivationService.RateAsync(_goalId, m3.Id, new RateMotivationRequest(1), _userId);
            // m4 has no rating

            var filter = new MotivationFilterRequest(minRating: 3);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().HaveCount(2);
            result.Items.Should().OnlyContain(m => m.Rating.HasValue && m.Rating.Value >= 3);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_MinRating5_ReturnsOnlyFiveStars()
        {
            var m1 = await AddMotivationAsync("Phrase 1");
            var m2 = await AddMotivationAsync("Phrase 2");

            await _motivationService.RateAsync(_goalId, m1.Id, new RateMotivationRequest(5), _userId);
            await _motivationService.RateAsync(_goalId, m2.Id, new RateMotivationRequest(4), _userId);

            var filter = new MotivationFilterRequest(minRating: 5);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().HaveCount(1);
            result.Items[0].Rating.Should().Be(5);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_MinRatingNull_ReturnsAll()
        {
            var m1 = await AddMotivationAsync("Phrase 1");
            var m2 = await AddMotivationAsync("Phrase 2");
            await _motivationService.RateAsync(_goalId, m1.Id, new RateMotivationRequest(2), _userId);

            var filter = new MotivationFilterRequest(minRating: null);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_MinRating_ExcludesUnrated()
        {
            var m1 = await AddMotivationAsync("Phrase 1");
            var m2 = await AddMotivationAsync("Phrase 2");
            await _motivationService.RateAsync(_goalId, m1.Id, new RateMotivationRequest(4), _userId);
            // m2 has no rating

            var filter = new MotivationFilterRequest(minRating: 1);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().HaveCount(1);
            result.Items[0].Id.Should().Be(m1.Id);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_MinRatingWithOnlyFavorites_CombinesFilters()
        {
            var m1 = await AddMotivationAsync("Phrase 1");
            var m2 = await AddMotivationAsync("Phrase 2");
            var m3 = await AddMotivationAsync("Phrase 3");

            await _motivationService.RateAsync(_goalId, m1.Id, new RateMotivationRequest(5), _userId);
            await _motivationService.FavoriteAsync(_goalId, m1.Id, _userId);

            await _motivationService.RateAsync(_goalId, m2.Id, new RateMotivationRequest(5), _userId);
            // m2 not favorite

            await _motivationService.FavoriteAsync(_goalId, m3.Id, _userId);
            // m3 favorite but no rating

            var filter = new MotivationFilterRequest(minRating: 4, onlyFavorites: true);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().HaveCount(1);
            result.Items[0].Id.Should().Be(m1.Id);
        }

        // ── MotivationFilterRequest: MinRating property ───────────────────────────

        [Fact]
        public void MotivationFilterRequest_NullMinRating_IsNull()
        {
            var req = new MotivationFilterRequest(minRating: null);
            req.MinRating.Should().BeNull();
        }

        [Fact]
        public void MotivationFilterRequest_MinRating3_Is3()
        {
            var req = new MotivationFilterRequest(minRating: 3);
            req.MinRating.Should().Be(3);
        }
    }
}
