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
    public class MotivationFavoriteTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly GoalRepository _goalRepository;
        private readonly MotivationRepository _motivationRepository;
        private readonly MotivationService _motivationService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _goalId;

        public MotivationFavoriteTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_MotivationFavorite_" + Guid.NewGuid())
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

        // ── Domain: Favorite/Unfavorite ──────────────────────────────────────────

        [Fact]
        public void Motivation_DefaultIsFavorite_IsFalse()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            m.IsFavorite.Should().BeFalse();
        }

        [Fact]
        public void Motivation_Favorite_SetsIsFavoriteTrue()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            m.Favorite();

            m.IsFavorite.Should().BeTrue();
        }

        [Fact]
        public void Motivation_Unfavorite_SetsIsFavoriteFalse()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");
            m.Favorite();

            m.Unfavorite();

            m.IsFavorite.Should().BeFalse();
        }

        [Fact]
        public void Motivation_FavoriteAlreadyFavorite_ThrowsInvalidOperationException()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");
            m.Favorite();

            Action act = () => m.Favorite();

            act.Should().Throw<InvalidOperationException>().WithMessage("*already a favorite*");
        }

        [Fact]
        public void Motivation_UnfavoriteNotFavorite_ThrowsInvalidOperationException()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text");

            Action act = () => m.Unfavorite();

            act.Should().Throw<InvalidOperationException>().WithMessage("*not a favorite*");
        }

        [Fact]
        public void Motivation_Constructor_WithIsFavoriteTrue_PreservesFlag()
        {
            var m = new Domain.Entities.Motivation(Guid.NewGuid(), _goalId, "text", isFavorite: true);

            m.IsFavorite.Should().BeTrue();
        }

        // ── Application: FavoriteAsync ────────────────────────────────────────────

        [Fact]
        public async Task FavoriteAsync_ValidMotivation_ReturnsIsFavoriteTrue()
        {
            var added = await AddMotivationAsync();

            var result = await _motivationService.FavoriteAsync(_goalId, added.Id, _userId);

            result.IsFavorite.Should().BeTrue();
        }

        [Fact]
        public async Task FavoriteAsync_PersistsToRepository()
        {
            var added = await AddMotivationAsync();

            await _motivationService.FavoriteAsync(_goalId, added.Id, _userId);

            var stored = await _motivationRepository.GetByIdAsync(added.Id);
            stored!.IsFavorite.Should().BeTrue();
        }

        [Fact]
        public async Task FavoriteAsync_GoalNotFound_ThrowsArgumentException()
        {
            var added = await AddMotivationAsync();

            Func<Task> act = async () => await _motivationService.FavoriteAsync(Guid.NewGuid(), added.Id, _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task FavoriteAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            var added = await AddMotivationAsync();

            Func<Task> act = async () => await _motivationService.FavoriteAsync(_goalId, added.Id, Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task FavoriteAsync_MotivationNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _motivationService.FavoriteAsync(_goalId, Guid.NewGuid(), _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Motivation not found*");
        }

        [Fact]
        public async Task FavoriteAsync_AlreadyFavorite_ThrowsInvalidOperationException()
        {
            var added = await AddMotivationAsync();
            await _motivationService.FavoriteAsync(_goalId, added.Id, _userId);

            Func<Task> act = async () => await _motivationService.FavoriteAsync(_goalId, added.Id, _userId);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already a favorite*");
        }

        // ── Application: UnfavoriteAsync ─────────────────────────────────────────

        [Fact]
        public async Task UnfavoriteAsync_FavoritedMotivation_ReturnsIsFavoriteFalse()
        {
            var added = await AddMotivationAsync();
            await _motivationService.FavoriteAsync(_goalId, added.Id, _userId);

            var result = await _motivationService.UnfavoriteAsync(_goalId, added.Id, _userId);

            result.IsFavorite.Should().BeFalse();
        }

        [Fact]
        public async Task UnfavoriteAsync_PersistsToRepository()
        {
            var added = await AddMotivationAsync();
            await _motivationService.FavoriteAsync(_goalId, added.Id, _userId);

            await _motivationService.UnfavoriteAsync(_goalId, added.Id, _userId);

            var stored = await _motivationRepository.GetByIdAsync(added.Id);
            stored!.IsFavorite.Should().BeFalse();
        }

        [Fact]
        public async Task UnfavoriteAsync_GoalNotFound_ThrowsArgumentException()
        {
            var added = await AddMotivationAsync();

            Func<Task> act = async () => await _motivationService.UnfavoriteAsync(Guid.NewGuid(), added.Id, _userId);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Goal not found*");
        }

        [Fact]
        public async Task UnfavoriteAsync_WrongUser_ThrowsUnauthorizedAccessException()
        {
            var added = await AddMotivationAsync();
            await _motivationService.FavoriteAsync(_goalId, added.Id, _userId);

            Func<Task> act = async () => await _motivationService.UnfavoriteAsync(_goalId, added.Id, Guid.NewGuid());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task UnfavoriteAsync_NotFavorite_ThrowsInvalidOperationException()
        {
            var added = await AddMotivationAsync();

            Func<Task> act = async () => await _motivationService.UnfavoriteAsync(_goalId, added.Id, _userId);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not a favorite*");
        }

        // ── Application: AddAsync returns IsFavorite=false ────────────────────────

        [Fact]
        public async Task AddAsync_NewMotivation_IsFavoriteFalseInResponse()
        {
            var result = await AddMotivationAsync();

            result.IsFavorite.Should().BeFalse();
        }

        // ── Application: ListByGoalFilteredAsync with OnlyFavorites ──────────────

        [Fact]
        public async Task ListByGoalFilteredAsync_OnlyFavoritesTrue_ReturnsOnlyFavorites()
        {
            var m1 = await AddMotivationAsync("Phrase 1");
            var m2 = await AddMotivationAsync("Phrase 2");
            var m3 = await AddMotivationAsync("Phrase 3");

            await _motivationService.FavoriteAsync(_goalId, m1.Id, _userId);
            await _motivationService.FavoriteAsync(_goalId, m3.Id, _userId);

            var filter = new MotivationFilterRequest(onlyFavorites: true);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().HaveCount(2);
            result.Items.Should().OnlyContain(m => m.IsFavorite);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_OnlyFavoritesFalse_ReturnsAll()
        {
            var m1 = await AddMotivationAsync("Phrase 1");
            var m2 = await AddMotivationAsync("Phrase 2");
            await _motivationService.FavoriteAsync(_goalId, m1.Id, _userId);

            var filter = new MotivationFilterRequest(onlyFavorites: false);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_OnlyFavoritesNull_ReturnsAll()
        {
            var m1 = await AddMotivationAsync("Phrase 1");
            var m2 = await AddMotivationAsync("Phrase 2");
            await _motivationService.FavoriteAsync(_goalId, m1.Id, _userId);

            var filter = new MotivationFilterRequest(onlyFavorites: null);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_OnlyFavoritesTrue_NoFavorites_ReturnsEmpty()
        {
            await AddMotivationAsync("Phrase 1");
            await AddMotivationAsync("Phrase 2");

            var filter = new MotivationFilterRequest(onlyFavorites: true);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task ListByGoalFilteredAsync_OnlyFavoritesWithSearch_CombinesFilters()
        {
            var m1 = await AddMotivationAsync("Keep going strong");
            var m2 = await AddMotivationAsync("Keep going fast");
            var m3 = await AddMotivationAsync("Stay focused");

            await _motivationService.FavoriteAsync(_goalId, m1.Id, _userId);
            await _motivationService.FavoriteAsync(_goalId, m3.Id, _userId);

            var filter = new MotivationFilterRequest(search: "Keep going", onlyFavorites: true);
            var result = await _motivationService.ListByGoalFilteredAsync(_goalId, _userId, filter);

            result.Items.Should().HaveCount(1);
            result.Items[0].Text.Should().Be("Keep going strong");
            result.Items[0].IsFavorite.Should().BeTrue();
        }

        // ── MotivationFilterRequest: OnlyFavorites property ───────────────────────

        [Fact]
        public void MotivationFilterRequest_NullOnlyFavorites_IsNull()
        {
            var req = new MotivationFilterRequest(onlyFavorites: null);
            req.OnlyFavorites.Should().BeNull();
        }

        [Fact]
        public void MotivationFilterRequest_OnlyFavoritesTrue_IsTrue()
        {
            var req = new MotivationFilterRequest(onlyFavorites: true);
            req.OnlyFavorites.Should().BeTrue();
        }

        [Fact]
        public void MotivationFilterRequest_OnlyFavoritesFalse_IsFalse()
        {
            var req = new MotivationFilterRequest(onlyFavorites: false);
            req.OnlyFavorites.Should().BeFalse();
        }
    }
}
