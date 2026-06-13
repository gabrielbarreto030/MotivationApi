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
    public class WeeklyReportService : IWeeklyReportService
    {
        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<WeeklyReportService> _logger;

        public WeeklyReportService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<WeeklyReportService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        public async Task<WeeklyReportResponse> GetWeeklyReportAsync(Guid userId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var weekStart = today.AddDays(-6);

            var weekStartDt = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var weekEndDt = today.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

            // Initialize daily counters for all 7 days
            var stepCountByDay = new Dictionary<DateOnly, int>();
            for (var d = weekStart; d <= today; d = d.AddDays(1))
                stepCountByDay[d] = 0;

            var goalsProgressedSet = new HashSet<Guid>();

            var goals = await _goalRepository.GetByUserAsync(userId);
            foreach (var goal in goals)
            {
                var steps = await _stepRepository.GetByGoalAsync(goal.Id);
                foreach (var step in steps)
                {
                    if (!step.IsCompleted || !step.CompletedAt.HasValue)
                        continue;

                    var completedDate = DateOnly.FromDateTime(step.CompletedAt.Value.ToUniversalTime());
                    if (completedDate < weekStart || completedDate > today)
                        continue;

                    stepCountByDay[completedDate]++;
                    goalsProgressedSet.Add(goal.Id);
                }
            }

            var totalStepsCompleted = stepCountByDay.Values.Sum();
            var totalGoalsProgressed = goalsProgressedSet.Count;

            var dailyBreakdown = stepCountByDay
                .OrderBy(kv => kv.Key)
                .Select(kv => new DailyActivityEntry(kv.Key, kv.Value))
                .ToList();

            DateTime? mostActiveDay = null;
            if (totalStepsCompleted > 0)
            {
                var maxDay = stepCountByDay.MaxBy(kv => kv.Value).Key;
                mostActiveDay = maxDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            }

            var averageStepsPerDay = Math.Round(totalStepsCompleted / 7.0, 2);

            _logger.LogInformation(
                "Weekly report for user {UserId}: {TotalSteps} steps completed, {GoalsProgressed} goals progressed",
                userId, totalStepsCompleted, totalGoalsProgressed);

            return new WeeklyReportResponse(
                weekStartDt,
                weekEndDt,
                totalStepsCompleted,
                totalGoalsProgressed,
                dailyBreakdown,
                mostActiveDay,
                averageStepsPerDay);
        }
    }
}
