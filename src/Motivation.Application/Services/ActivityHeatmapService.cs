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
    public class ActivityHeatmapService : IActivityHeatmapService
    {
        private const int TotalDays = 365;

        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<ActivityHeatmapService> _logger;

        public ActivityHeatmapService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<ActivityHeatmapService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        public async Task<ActivityHeatmapResponse> GetHeatmapAsync(Guid userId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var windowStart = today.AddDays(-(TotalDays - 1)); // 364 days ago

            var windowStartDt = windowStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var windowEndDt = today.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

            // Initialize a count map for every day in the window (0-filled)
            var countByDay = new Dictionary<DateOnly, int>(TotalDays);
            for (var d = windowStart; d <= today; d = d.AddDays(1))
                countByDay[d] = 0;

            var goals = await _goalRepository.GetByUserAsync(userId);
            foreach (var goal in goals)
            {
                var steps = await _stepRepository.GetByGoalAsync(goal.Id);
                foreach (var step in steps)
                {
                    if (!step.IsCompleted || !step.CompletedAt.HasValue)
                        continue;

                    var completedDate = DateOnly.FromDateTime(step.CompletedAt.Value.ToUniversalTime());
                    if (completedDate < windowStart || completedDate > today)
                        continue;

                    countByDay[completedDate]++;
                }
            }

            var totalStepsCompleted = countByDay.Values.Sum();
            var activeDays = countByDay.Values.Count(c => c > 0);

            // Build entries ordered ascending by date; every day in the window is included
            var entries = countByDay
                .OrderBy(kv => kv.Key)
                .Select(kv => new HeatmapEntry(
                    kv.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    kv.Value))
                .ToList();

            _logger.LogInformation(
                "Activity heatmap for user {UserId}: {TotalSteps} steps over {ActiveDays} active days (365-day window)",
                userId, totalStepsCompleted, activeDays);

            return new ActivityHeatmapResponse(
                windowStartDt,
                windowEndDt,
                totalStepsCompleted,
                activeDays,
                entries);
        }
    }
}
