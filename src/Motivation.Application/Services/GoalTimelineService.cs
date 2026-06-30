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
    public class GoalTimelineService : IGoalTimelineService
    {
        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<GoalTimelineService> _logger;

        public GoalTimelineService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<GoalTimelineService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        public async Task<GoalTimelineResponse?> GetTimelineAsync(Guid userId, Guid goalId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null || goal.UserId != userId)
                return null;

            var steps = await _stepRepository.GetByGoalAsync(goalId);

            var entries = steps
                .Where(s => s.IsCompleted && s.CompletedAt.HasValue)
                .OrderBy(s => s.CompletedAt!.Value)
                .Select(s => new GoalTimelineEntry(s.Id, s.Title, s.CompletedAt!.Value))
                .ToList();

            _logger.LogInformation(
                "Goal timeline for goal {GoalId} (user {UserId}): {TotalSteps} steps, {CompletedSteps} completed",
                goalId, userId, steps.Length, entries.Count);

            return new GoalTimelineResponse(
                goal.Id,
                goal.Title,
                steps.Length,
                entries.Count,
                entries);
        }
    }
}
