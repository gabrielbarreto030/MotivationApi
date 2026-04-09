using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;
using Motivation.Domain.Entities;
using System.Linq;
using Motivation.Domain.Interfaces;

namespace Motivation.Application.Services
{
    public class GoalService : IGoalService
    {
        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly IGoalProgressCalculator _progressCalculator;
        private readonly ILogger<GoalService> _logger;

        public GoalService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            IGoalProgressCalculator progressCalculator,
            ILogger<GoalService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _progressCalculator = progressCalculator;
            _logger = logger;
        }

        public async Task<CreateGoalResponse> CreateAsync(CreateGoalRequest request, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required", nameof(request.Title));
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Description is required", nameof(request.Description));

            var goal = new Goal(Guid.NewGuid(), userId, request.Title, request.Description, GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            _logger.LogInformation("Goal {GoalId} created for user {UserId} with title '{Title}'", goal.Id, userId, goal.Title);

            return new CreateGoalResponse(goal.Id, goal.Title, goal.Description, goal.Status, goal.CreatedAt);
        }

        public async Task<CreateGoalResponse[]> ListByUserAsync(Guid userId)
        {
            var goals = await _goalRepository.GetByUserAsync(userId);
            _logger.LogInformation("Listed {Count} goals for user {UserId}", goals.Length, userId);
            return goals.Select(g => new CreateGoalResponse(g.Id, g.Title, g.Description, g.Status, g.CreatedAt)).ToArray();
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

            goal.Update(request.Title, request.Description, status);
            await _goalRepository.UpdateAsync(goal);

            _logger.LogInformation("Goal {GoalId} updated by user {UserId}", id, userId);

            return new UpdateGoalResponse(goal.Id, goal.Title, goal.Description, goal.Status, goal.CreatedAt);
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
            double percentage = _progressCalculator.Calculate(total, completed);

            _logger.LogInformation("Progress for goal {GoalId}: {Completed}/{Total} steps ({Percentage}%)", goalId, completed, total, percentage);

            return new GoalProgressResponse(goalId, total, completed, percentage);
        }
    }
}
