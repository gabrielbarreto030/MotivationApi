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
    public class StepService : IStepService
    {
        private readonly IStepRepository _stepRepository;
        private readonly IGoalRepository _goalRepository;
        private readonly ILogger<StepService> _logger;

        public StepService(IStepRepository stepRepository, IGoalRepository goalRepository, ILogger<StepService> logger)
        {
            _stepRepository = stepRepository;
            _goalRepository = goalRepository;
            _logger = logger;
        }

        public async Task<CreateStepResponse> CreateAsync(Guid goalId, CreateStepRequest request, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required", nameof(request.Title));

            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to add steps to this goal");

            var step = new Step(Guid.NewGuid(), goalId, request.Title, request.Notes, request.DueDate);
            await _stepRepository.AddAsync(step);

            _logger.LogInformation("Step {StepId} created for goal {GoalId} by user {UserId}", step.Id, goalId, userId);

            var now = DateTime.UtcNow;
            return new CreateStepResponse(step.Id, step.GoalId, step.Title, step.IsCompleted, step.CompletedAt, step.Notes, step.DueDate, step.IsOverdue(now));
        }

        public async Task<CreateStepResponse[]> ListByGoalAsync(Guid goalId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to view steps of this goal");

            var steps = await _stepRepository.GetByGoalAsync(goalId);
            var now = DateTime.UtcNow;
            _logger.LogInformation("Listed {Count} steps for goal {GoalId} by user {UserId}", steps.Length, goalId, userId);
            return steps.Select(s => new CreateStepResponse(s.Id, s.GoalId, s.Title, s.IsCompleted, s.CompletedAt, s.Notes, s.DueDate, s.IsOverdue(now))).ToArray();
        }

        public async Task<PagedResponse<CreateStepResponse>> ListByGoalPagedAsync(Guid goalId, Guid userId, PagedRequest request)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to view steps of this goal");

            var steps = await _stepRepository.GetByGoalAsync(goalId);
            var now = DateTime.UtcNow;
            var totalCount = steps.Length;
            var items = steps
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(s => new CreateStepResponse(s.Id, s.GoalId, s.Title, s.IsCompleted, s.CompletedAt, s.Notes, s.DueDate, s.IsOverdue(now)))
                .ToArray();

            _logger.LogInformation(
                "Listed {Count}/{Total} steps (page {Page}, pageSize {PageSize}) for goal {GoalId} by user {UserId}",
                items.Length, totalCount, request.Page, request.PageSize, goalId, userId);

            return new PagedResponse<CreateStepResponse>(items, totalCount, request.Page, request.PageSize);
        }

        public async Task<PagedResponse<CreateStepResponse>> ListByGoalFilteredAsync(Guid goalId, Guid userId, StepFilterRequest request)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to view steps of this goal");

            var steps = await _stepRepository.GetByGoalAsync(goalId);

            IEnumerable<Step> filtered = request.IsCompleted.HasValue
                ? steps.Where(s => s.IsCompleted == request.IsCompleted.Value)
                : steps;

            IEnumerable<Step> sorted = request.SortBy switch
            {
                "iscompleted" => request.SortOrder == "desc"
                    ? filtered.OrderByDescending(s => s.IsCompleted)
                    : filtered.OrderBy(s => s.IsCompleted),
                "completedat" => request.SortOrder == "desc"
                    ? filtered.OrderByDescending(s => s.CompletedAt)
                    : filtered.OrderBy(s => s.CompletedAt),
                _ => request.SortOrder == "desc"
                    ? filtered.OrderByDescending(s => s.Title)
                    : filtered.OrderBy(s => s.Title)
            };

            var now = DateTime.UtcNow;
            var sortedList = sorted.ToArray();
            var totalCount = sortedList.Length;
            var items = sortedList
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(s => new CreateStepResponse(s.Id, s.GoalId, s.Title, s.IsCompleted, s.CompletedAt, s.Notes, s.DueDate, s.IsOverdue(now)))
                .ToArray();

            _logger.LogInformation(
                "Listed {Count}/{Total} steps (page {Page}, pageSize {PageSize}, isCompleted: {IsCompleted}, sortBy: {SortBy}, sortOrder: {SortOrder}) for goal {GoalId} by user {UserId}",
                items.Length, totalCount, request.Page, request.PageSize,
                request.IsCompleted?.ToString() ?? "all", request.SortBy, request.SortOrder, goalId, userId);

            return new PagedResponse<CreateStepResponse>(items, totalCount, request.Page, request.PageSize);
        }

        public async Task<CreateStepResponse> MarkCompletedAsync(Guid goalId, Guid stepId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to update steps of this goal");

            var step = await _stepRepository.GetByIdAsync(stepId);
            if (step == null)
                throw new ArgumentException("Step not found", nameof(stepId));

            if (step.GoalId != goalId)
                throw new ArgumentException("Step does not belong to the specified goal", nameof(stepId));

            if (step.IsCompleted)
                throw new InvalidOperationException("Step is already completed");

            var completedAt = DateTime.UtcNow;
            step.MarkCompleted(completedAt);
            await _stepRepository.UpdateAsync(step);

            _logger.LogInformation("Step {StepId} marked as completed for goal {GoalId} by user {UserId}", stepId, goalId, userId);

            return new CreateStepResponse(step.Id, step.GoalId, step.Title, step.IsCompleted, step.CompletedAt, step.Notes, step.DueDate, step.IsOverdue(completedAt));
        }

        public async Task<CreateStepResponse> UpdateAsync(Guid goalId, Guid stepId, UpdateStepRequest request, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to update steps of this goal");

            var step = await _stepRepository.GetByIdAsync(stepId);
            if (step == null)
                throw new ArgumentException("Step not found", nameof(stepId));

            if (step.GoalId != goalId)
                throw new ArgumentException("Step does not belong to the specified goal", nameof(stepId));

            if (!string.IsNullOrWhiteSpace(request.Title))
                step.UpdateTitle(request.Title);

            if (request.ClearNotes)
                step.UpdateNotes(null);
            else if (request.Notes != null)
                step.UpdateNotes(request.Notes);

            if (request.ClearDueDate)
                step.UpdateDueDate(null);
            else if (request.DueDate.HasValue)
                step.UpdateDueDate(request.DueDate.Value);

            await _stepRepository.UpdateAsync(step);

            _logger.LogInformation("Step {StepId} updated for goal {GoalId} by user {UserId}", stepId, goalId, userId);

            var now = DateTime.UtcNow;
            return new CreateStepResponse(step.Id, step.GoalId, step.Title, step.IsCompleted, step.CompletedAt, step.Notes, step.DueDate, step.IsOverdue(now));
        }

        public async Task<CreateStepResponse[]> GetOverdueByGoalAsync(Guid goalId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to view steps of this goal");

            var now = DateTime.UtcNow;
            var steps = await _stepRepository.GetByGoalAsync(goalId);
            var overdue = steps.Where(s => s.IsOverdue(now)).ToArray();

            _logger.LogInformation("Found {Count} overdue steps for goal {GoalId} by user {UserId}", overdue.Length, goalId, userId);

            return overdue.Select(s => new CreateStepResponse(s.Id, s.GoalId, s.Title, s.IsCompleted, s.CompletedAt, s.Notes, s.DueDate, true)).ToArray();
        }
    }
}
