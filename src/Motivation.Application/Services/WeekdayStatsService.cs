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
    public class WeekdayStatsService : IWeekdayStatsService
    {
        // Monday=1 through Sunday=7 (ISO order)
        private static readonly string[] DayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<WeekdayStatsService> _logger;

        public WeekdayStatsService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<WeekdayStatsService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        public async Task<WeekdayStatsResponse> GetWeekdayStatsAsync(Guid userId)
        {
            var goals = await _goalRepository.GetByUserAsync(userId);

            // count[0] = Monday, count[6] = Sunday
            var counts = new int[7];

            foreach (var goal in goals)
            {
                var steps = await _stepRepository.GetByGoalAsync(goal.Id);
                foreach (var step in steps)
                {
                    if (!step.IsCompleted || !step.CompletedAt.HasValue)
                        continue;

                    var dow = step.CompletedAt.Value.ToUniversalTime().DayOfWeek;
                    // DayOfWeek: Sunday=0, Monday=1 ... Saturday=6
                    // Map to Monday-based index: Monday=0 ... Sunday=6
                    var index = dow == DayOfWeek.Sunday ? 6 : (int)dow - 1;
                    counts[index]++;
                }
            }

            var entries = DayNames
                .Select((name, i) => new WeekdayEntry(name, counts[i]))
                .ToList();

            var total = counts.Sum();

            string? mostProductiveDay = null;
            if (total > 0)
            {
                var maxCount = counts.Max();
                var maxIndex = Array.IndexOf(counts, maxCount);
                mostProductiveDay = DayNames[maxIndex];
            }

            _logger.LogInformation(
                "Weekday stats for user {UserId}: {TotalSteps} completed steps, most productive day: {MostProductiveDay}",
                userId, total, mostProductiveDay ?? "none");

            return new WeekdayStatsResponse(total, mostProductiveDay, entries);
        }
    }
}
