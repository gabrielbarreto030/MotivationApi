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
    public class DailySummaryService : IDailySummaryService
    {
        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<DailySummaryService> _logger;

        public DailySummaryService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<DailySummaryService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        public async Task<DailySummaryResponse> GetDailySummaryAsync(Guid userId, DateOnly date)
        {
            var goals = await _goalRepository.GetByUserAsync(userId);

            var goalEntries = new List<DailySummaryGoalEntry>();

            foreach (var goal in goals)
            {
                var steps = await _stepRepository.GetByGoalAsync(goal.Id);

                var stepsOnDate = steps
                    .Where(s => s.IsCompleted && s.CompletedAt.HasValue
                        && DateOnly.FromDateTime(s.CompletedAt.Value.ToUniversalTime()) == date)
                    .OrderBy(s => s.CompletedAt!.Value)
                    .Select(s => new DailySummaryStepEntry(s.Id, s.Title, s.CompletedAt!.Value))
                    .ToList();

                if (stepsOnDate.Count > 0)
                    goalEntries.Add(new DailySummaryGoalEntry(goal.Id, goal.Title, stepsOnDate));
            }

            goalEntries = goalEntries.OrderBy(e => e.GoalTitle).ToList();

            var totalSteps = goalEntries.Sum(e => e.Steps.Count);
            var goalsProgressed = goalEntries.Count;

            _logger.LogInformation(
                "Daily summary for user {UserId} on {Date}: {TotalSteps} steps across {GoalsProgressed} goals",
                userId, date, totalSteps, goalsProgressed);

            return new DailySummaryResponse(date, totalSteps, goalsProgressed, goalEntries);
        }
    }
}
