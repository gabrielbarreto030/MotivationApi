using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;
using Motivation.Domain.Interfaces;

namespace Motivation.Application.Services
{
    public class MotivationService : IMotivationService
    {
        private readonly IMotivationRepository _motivationRepository;
        private readonly IGoalRepository _goalRepository;
        private readonly ILogger<MotivationService> _logger;

        public MotivationService(IMotivationRepository motivationRepository, IGoalRepository goalRepository, ILogger<MotivationService> logger)
        {
            _motivationRepository = motivationRepository;
            _goalRepository = goalRepository;
            _logger = logger;
        }

        public async Task<AddMotivationResponse> AddAsync(Guid goalId, AddMotivationRequest request, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                throw new ArgumentException("Text is required", nameof(request.Text));

            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to add motivations to this goal");

            var motivation = new Domain.Entities.Motivation(Guid.NewGuid(), goalId, request.Text, DateTime.UtcNow);
            if (request.Tags != null)
                motivation.SetTags(request.Tags);
            await _motivationRepository.AddAsync(motivation);

            _logger.LogInformation("Motivation {MotivationId} added to goal {GoalId} by user {UserId}", motivation.Id, goalId, userId);

            return new AddMotivationResponse(motivation.Id, motivation.GoalId, motivation.Text, motivation.CreatedAt, motivation.Tags, motivation.IsFavorite, motivation.Rating);
        }

        public async Task RemoveAsync(Guid goalId, Guid motivationId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to remove motivations from this goal");

            var motivation = await _motivationRepository.GetByIdAsync(motivationId);
            if (motivation == null)
                throw new ArgumentException("Motivation not found", nameof(motivationId));

            if (motivation.GoalId != goalId)
                throw new ArgumentException("Motivation does not belong to this goal", nameof(motivationId));

            await _motivationRepository.DeleteAsync(motivationId);

            _logger.LogInformation("Motivation {MotivationId} removed from goal {GoalId} by user {UserId}", motivationId, goalId, userId);
        }

        public async Task<AddMotivationResponse[]> ListByGoalAsync(Guid goalId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to view motivations of this goal");

            var motivations = await _motivationRepository.GetByGoalAsync(goalId);

            _logger.LogInformation("Listed {Count} motivations for goal {GoalId} by user {UserId}", motivations.Length, goalId, userId);

            return System.Array.ConvertAll(motivations, m => new AddMotivationResponse(m.Id, m.GoalId, m.Text, m.CreatedAt, m.Tags, m.IsFavorite, m.Rating));
        }

        public async Task<PagedResponse<AddMotivationResponse>> ListByGoalFilteredAsync(Guid goalId, Guid userId, MotivationFilterRequest filter)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to view motivations of this goal");

            var motivations = await _motivationRepository.GetByGoalAsync(goalId);

            IEnumerable<Domain.Entities.Motivation> query = motivations;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.ToLowerInvariant();
                query = query.Where(m => m.Text.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filter.Tag))
            {
                var tagLower = filter.Tag.ToLowerInvariant();
                query = query.Where(m => m.Tags.Any(t => t.Equals(tagLower, StringComparison.OrdinalIgnoreCase)));
            }

            if (filter.OnlyFavorites == true)
                query = query.Where(m => m.IsFavorite);

            if (filter.MinRating.HasValue)
                query = query.Where(m => m.Rating.HasValue && m.Rating.Value >= filter.MinRating.Value);

            query = (filter.SortBy, filter.SortOrder) switch
            {
                ("text", "desc") => query.OrderByDescending(m => m.Text),
                ("text", _) => query.OrderBy(m => m.Text),
                ("createdat", "desc") => query.OrderByDescending(m => m.CreatedAt),
                _ => query.OrderBy(m => m.CreatedAt),
            };

            var filtered = query.ToArray();
            var total = filtered.Length;
            var items = filtered
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(m => new AddMotivationResponse(m.Id, m.GoalId, m.Text, m.CreatedAt, m.Tags, m.IsFavorite, m.Rating))
                .ToArray();

            _logger.LogInformation(
                "Listed {Count}/{Total} motivations for goal {GoalId} by user {UserId} (search={Search}, sortBy={SortBy}, sortOrder={SortOrder}, tag={Tag}, onlyFavorites={OnlyFavorites}, minRating={MinRating})",
                items.Length, total, goalId, userId, filter.Search, filter.SortBy, filter.SortOrder, filter.Tag, filter.OnlyFavorites, filter.MinRating);

            return new PagedResponse<AddMotivationResponse>(items, total, filter.Page, filter.PageSize);
        }

        public async Task<AddMotivationResponse> UpdateAsync(Guid goalId, Guid motivationId, UpdateMotivationRequest request, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                throw new ArgumentException("Text is required", nameof(request.Text));

            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to update motivations of this goal");

            var motivation = await _motivationRepository.GetByIdAsync(motivationId);
            if (motivation == null)
                throw new ArgumentException("Motivation not found", nameof(motivationId));

            if (motivation.GoalId != goalId)
                throw new ArgumentException("Motivation does not belong to this goal", nameof(motivationId));

            motivation.UpdateText(request.Text);
            if (request.ClearTags)
                motivation.SetTags(null);
            else if (request.Tags != null)
                motivation.SetTags(request.Tags);
            await _motivationRepository.UpdateAsync(motivation);

            _logger.LogInformation("Motivation {MotivationId} updated on goal {GoalId} by user {UserId}", motivationId, goalId, userId);

            return new AddMotivationResponse(motivation.Id, motivation.GoalId, motivation.Text, motivation.CreatedAt, motivation.Tags, motivation.IsFavorite, motivation.Rating);
        }

        public async Task<AddMotivationResponse> FavoriteAsync(Guid goalId, Guid motivationId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to favorite motivations of this goal");

            var motivation = await _motivationRepository.GetByIdAsync(motivationId);
            if (motivation == null)
                throw new ArgumentException("Motivation not found", nameof(motivationId));

            if (motivation.GoalId != goalId)
                throw new ArgumentException("Motivation does not belong to this goal", nameof(motivationId));

            motivation.Favorite();
            await _motivationRepository.UpdateAsync(motivation);

            _logger.LogInformation("Motivation {MotivationId} marked as favorite on goal {GoalId} by user {UserId}", motivationId, goalId, userId);

            return new AddMotivationResponse(motivation.Id, motivation.GoalId, motivation.Text, motivation.CreatedAt, motivation.Tags, motivation.IsFavorite, motivation.Rating);
        }

        public async Task<AddMotivationResponse> UnfavoriteAsync(Guid goalId, Guid motivationId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to unfavorite motivations of this goal");

            var motivation = await _motivationRepository.GetByIdAsync(motivationId);
            if (motivation == null)
                throw new ArgumentException("Motivation not found", nameof(motivationId));

            if (motivation.GoalId != goalId)
                throw new ArgumentException("Motivation does not belong to this goal", nameof(motivationId));

            motivation.Unfavorite();
            await _motivationRepository.UpdateAsync(motivation);

            _logger.LogInformation("Motivation {MotivationId} removed from favorites on goal {GoalId} by user {UserId}", motivationId, goalId, userId);

            return new AddMotivationResponse(motivation.Id, motivation.GoalId, motivation.Text, motivation.CreatedAt, motivation.Tags, motivation.IsFavorite, motivation.Rating);
        }

        public async Task<AddMotivationResponse> RateAsync(Guid goalId, Guid motivationId, RateMotivationRequest request, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to rate motivations of this goal");

            var motivation = await _motivationRepository.GetByIdAsync(motivationId);
            if (motivation == null)
                throw new ArgumentException("Motivation not found", nameof(motivationId));

            if (motivation.GoalId != goalId)
                throw new ArgumentException("Motivation does not belong to this goal", nameof(motivationId));

            motivation.Rate(request.Rating);
            await _motivationRepository.UpdateAsync(motivation);

            _logger.LogInformation("Motivation {MotivationId} rated {Rating} on goal {GoalId} by user {UserId}", motivationId, request.Rating, goalId, userId);

            return new AddMotivationResponse(motivation.Id, motivation.GoalId, motivation.Text, motivation.CreatedAt, motivation.Tags, motivation.IsFavorite, motivation.Rating);
        }

        public async Task<MotivationStatsResponse> GetStatsAsync(Guid goalId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to view stats of this goal");

            var motivations = await _motivationRepository.GetByGoalAsync(goalId);

            var totalMotivations = motivations.Length;
            var totalFavorites = motivations.Count(m => m.IsFavorite);
            var rated = motivations.Where(m => m.Rating.HasValue).ToArray();
            var ratedMotivations = rated.Length;
            var averageRating = ratedMotivations > 0 ? (double?)rated.Average(m => m.Rating!.Value) : null;

            var tagBreakdown = motivations
                .SelectMany(m => m.Tags)
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation(
                "Stats for goal {GoalId} by user {UserId}: total={Total}, favorites={Favorites}, rated={Rated}, avgRating={AvgRating}",
                goalId, userId, totalMotivations, totalFavorites, ratedMotivations, averageRating);

            return new MotivationStatsResponse(totalMotivations, totalFavorites, ratedMotivations, averageRating, tagBreakdown);
        }

        public async Task<AddMotivationResponse> ClearRatingAsync(Guid goalId, Guid motivationId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to clear rating of motivations of this goal");

            var motivation = await _motivationRepository.GetByIdAsync(motivationId);
            if (motivation == null)
                throw new ArgumentException("Motivation not found", nameof(motivationId));

            if (motivation.GoalId != goalId)
                throw new ArgumentException("Motivation does not belong to this goal", nameof(motivationId));

            motivation.ClearRating();
            await _motivationRepository.UpdateAsync(motivation);

            _logger.LogInformation("Motivation {MotivationId} rating cleared on goal {GoalId} by user {UserId}", motivationId, goalId, userId);

            return new AddMotivationResponse(motivation.Id, motivation.GoalId, motivation.Text, motivation.CreatedAt, motivation.Tags, motivation.IsFavorite, motivation.Rating);
        }
    }
}
