using System;
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

            var step = new Step(Guid.NewGuid(), goalId, request.Title);
            await _stepRepository.AddAsync(step);

            _logger.LogInformation("Step {StepId} created for goal {GoalId} by user {UserId}", step.Id, goalId, userId);

            return new CreateStepResponse(step.Id, step.GoalId, step.Title, step.IsCompleted, step.CompletedAt);
        }

        public async Task<CreateStepResponse[]> ListByGoalAsync(Guid goalId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to view steps of this goal");

            var steps = await _stepRepository.GetByGoalAsync(goalId);
            _logger.LogInformation("Listed {Count} steps for goal {GoalId} by user {UserId}", steps.Length, goalId, userId);
            return steps.Select(s => new CreateStepResponse(s.Id, s.GoalId, s.Title, s.IsCompleted, s.CompletedAt)).ToArray();
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

            step.MarkCompleted(DateTime.UtcNow);
            await _stepRepository.UpdateAsync(step);

            _logger.LogInformation("Step {StepId} marked as completed for goal {GoalId} by user {UserId}", stepId, goalId, userId);

            return new CreateStepResponse(step.Id, step.GoalId, step.Title, step.IsCompleted, step.CompletedAt);
        }
    }
}
