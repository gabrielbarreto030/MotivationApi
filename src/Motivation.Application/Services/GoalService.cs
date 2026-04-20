using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;
using Motivation.Domain.Entities;
using Motivation.Domain.Interfaces;

namespace Motivation.Application.Services
{
    public class GoalService : IGoalService
    {
        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<GoalService> _logger;

        public GoalService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<GoalService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        private static CreateGoalResponse ToCreateResponse(Goal g, DateTime now) =>
            new CreateGoalResponse(g.Id, g.Title, g.Description, g.Status, g.Priority, g.CreatedAt, g.Deadline, g.IsOverdue(now), g.Notes);

        private static UpdateGoalResponse ToUpdateResponse(Goal g, DateTime now) =>
            new UpdateGoalResponse(g.Id, g.Title, g.Description, g.Status, g.Priority, g.CreatedAt, g.Deadline, g.IsOverdue(now), g.Notes);

        public async Task<CreateGoalResponse> CreateAsync(CreateGoalRequest request, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required", nameof(request.Title));
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Description is required", nameof(request.Description));

            var now = DateTime.UtcNow;
            var goal = new Goal(Guid.NewGuid(), userId, request.Title, request.Description, GoalStatus.Pending, now, request.Deadline, request.Priority, request.Notes);
            await _goalRepository.AddAsync(goal);

            _logger.LogInformation("Goal {GoalId} created for user {UserId} with title '{Title}'", goal.Id, userId, goal.Title);

            return ToCreateResponse(goal, now);
        }

        public async Task<CreateGoalResponse[]> ListByUserAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            var goals = await _goalRepository.GetByUserAsync(userId);
            _logger.LogInformation("Listed {Count} goals for user {UserId}", goals.Length, userId);
            return goals.Select(g => ToCreateResponse(g, now)).ToArray();
        }

        public async Task<PagedResponse<CreateGoalResponse>> ListByUserPagedAsync(Guid userId, PagedRequest request)
        {
            var now = DateTime.UtcNow;
            var goals = await _goalRepository.GetByUserAsync(userId);
            var totalCount = goals.Length;
            var items = goals
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(g => ToCreateResponse(g, now))
                .ToArray();

            _logger.LogInformation(
                "Listed {Count}/{Total} goals (page {Page}, pageSize {PageSize}) for user {UserId}",
                items.Length, totalCount, request.Page, request.PageSize, userId);

            return new PagedResponse<CreateGoalResponse>(items, totalCount, request.Page, request.PageSize);
        }

        public async Task<PagedResponse<CreateGoalResponse>> ListByUserFilteredAsync(Guid userId, GoalFilterRequest request)
        {
            var now = DateTime.UtcNow;
            var goals = await _goalRepository.GetByUserAsync(userId);

            IEnumerable<Goal> filtered = goals;
            if (request.Status.HasValue)
                filtered = filtered.Where(g => g.Status == request.Status.Value);
            if (request.Priority.HasValue)
                filtered = filtered.Where(g => g.Priority == request.Priority.Value);

            IEnumerable<Goal> sorted = request.SortBy switch
            {
                "title" => request.SortOrder == "desc"
                    ? filtered.OrderByDescending(g => g.Title)
                    : filtered.OrderBy(g => g.Title),
                "status" => request.SortOrder == "desc"
                    ? filtered.OrderByDescending(g => (int)g.Status)
                    : filtered.OrderBy(g => (int)g.Status),
                "priority" => request.SortOrder == "desc"
                    ? filtered.OrderByDescending(g => (int)g.Priority)
                    : filtered.OrderBy(g => (int)g.Priority),
                _ => request.SortOrder == "desc"
                    ? filtered.OrderByDescending(g => g.CreatedAt)
                    : filtered.OrderBy(g => g.CreatedAt)
            };

            var sortedList = sorted.ToArray();
            var totalCount = sortedList.Length;
            var items = sortedList
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(g => ToCreateResponse(g, now))
                .ToArray();

            _logger.LogInformation(
                "Listed {Count}/{Total} goals (page {Page}, pageSize {PageSize}, status: {Status}, priority: {Priority}, sortBy: {SortBy}, sortOrder: {SortOrder}) for user {UserId}",
                items.Length, totalCount, request.Page, request.PageSize,
                request.Status?.ToString() ?? "all", request.Priority?.ToString() ?? "all", request.SortBy, request.SortOrder, userId);

            return new PagedResponse<CreateGoalResponse>(items, totalCount, request.Page, request.PageSize);
        }

        public async Task<UpdateGoalResponse> UpdateAsync(Guid id, UpdateGoalRequest request, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(id);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(id));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to update this goal");

            GoalStatus? status = null;
            if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<GoalStatus>(request.Status, true, out var parsedStatus))
                status = parsedStatus;

            goal.Update(request.Title, request.Description, status, request.Deadline, request.ClearDeadline, request.Priority, request.Notes, request.ClearNotes);
            await _goalRepository.UpdateAsync(goal);

            _logger.LogInformation("Goal {GoalId} updated by user {UserId}", id, userId);

            var now = DateTime.UtcNow;
            return ToUpdateResponse(goal, now);
        }

        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(id);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(id));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to delete this goal");

            await _goalRepository.DeleteAsync(goal);

            _logger.LogInformation("Goal {GoalId} deleted by user {UserId}", id, userId);
        }

        public async Task<GoalProgressResponse> GetProgressAsync(Guid goalId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to view progress of this goal");

            var steps = await _stepRepository.GetByGoalAsync(goalId);
            int total = steps.Length;
            int completed = steps.Count(s => s.IsCompleted);
            double percentage = total == 0 ? 0 : Math.Round((double)completed / total * 100, 2);

            _logger.LogInformation("Progress for goal {GoalId}: {Completed}/{Total} steps ({Percentage}%)", goalId, completed, total, percentage);

            return new GoalProgressResponse(goalId, total, completed, percentage);
        }

        public async Task<CreateGoalResponse[]> GetOverdueAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            var goals = await _goalRepository.GetByUserAsync(userId);
            var overdue = goals.Where(g => g.IsOverdue(now)).ToArray();

            _logger.LogInformation("Found {Count} overdue goals for user {UserId}", overdue.Length, userId);

            return overdue.Select(g => ToCreateResponse(g, now)).ToArray();
        }

        public async Task<UserGoalsSummaryResponse> GetSummaryAsync(Guid userId)
        {
            var goals = await _goalRepository.GetByUserAsync(userId);

            int totalGoals = goals.Length;
            int pending = goals.Count(g => g.Status == GoalStatus.Pending);
            int inProgress = goals.Count(g => g.Status == GoalStatus.InProgress);
            int completed = goals.Count(g => g.Status == GoalStatus.Completed);
            int cancelled = goals.Count(g => g.Status == GoalStatus.Cancelled);

            int totalSteps = 0;
            int completedSteps = 0;
            foreach (var goal in goals)
            {
                var steps = await _stepRepository.GetByGoalAsync(goal.Id);
                totalSteps += steps.Length;
                completedSteps += steps.Count(s => s.IsCompleted);
            }

            double overallCompletionRate = totalSteps == 0
                ? 0
                : Math.Round((double)completedSteps / totalSteps * 100, 2);

            _logger.LogInformation(
                "Summary for user {UserId}: {TotalGoals} goals, {TotalSteps} steps, {CompletedSteps} completed ({Rate}%)",
                userId, totalGoals, totalSteps, completedSteps, overallCompletionRate);

            return new UserGoalsSummaryResponse(totalGoals, pending, inProgress, completed, cancelled, totalSteps, completedSteps, overallCompletionRate);
        }
    }
}
