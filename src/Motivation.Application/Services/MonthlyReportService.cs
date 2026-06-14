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
    public class MonthlyReportService : IMonthlyReportService
    {
        // The 30-day window is divided into 5 buckets of 6 days each.
        // Bucket 1 = oldest (days 29-24 ago), Bucket 5 = newest (days 5-0 ago, includes today).
        private const int TotalDays = 30;
        private const int BucketSize = 6;
        private const int BucketCount = TotalDays / BucketSize; // 5

        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<MonthlyReportService> _logger;

        public MonthlyReportService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<MonthlyReportService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        public async Task<MonthlyReportResponse> GetMonthlyReportAsync(Guid userId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var monthStart = today.AddDays(-(TotalDays - 1)); // 29 days ago

            var monthStartDt = monthStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var monthEndDt = today.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

            // Build 5 weekly buckets (6-day periods)
            var buckets = new int[BucketCount]; // buckets[0] = oldest

            // Track daily step counts for MostActiveDay
            var stepCountByDay = new Dictionary<DateOnly, int>();
            for (var d = monthStart; d <= today; d = d.AddDays(1))
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
                    if (completedDate < monthStart || completedDate > today)
                        continue;

                    stepCountByDay[completedDate]++;
                    goalsProgressedSet.Add(goal.Id);

                    // Assign to bucket: days elapsed from monthStart determines bucket index
                    var daysFromStart = completedDate.DayNumber - monthStart.DayNumber;
                    var bucketIndex = daysFromStart / BucketSize;
                    // Clamp to last bucket in case of rounding (shouldn't happen with exact 30 days)
                    if (bucketIndex >= BucketCount) bucketIndex = BucketCount - 1;
                    buckets[bucketIndex]++;
                }
            }

            var totalStepsCompleted = stepCountByDay.Values.Sum();
            var totalGoalsProgressed = goalsProgressedSet.Count;

            // Build WeeklyBreakdown (5 entries, WeekNumber 1 = oldest)
            var weeklyBreakdown = new List<WeeklyActivityEntry>(BucketCount);
            for (int i = 0; i < BucketCount; i++)
            {
                var wStart = monthStart.AddDays(i * BucketSize);
                var wEnd = wStart.AddDays(BucketSize - 1);
                if (wEnd > today) wEnd = today;
                weeklyBreakdown.Add(new WeeklyActivityEntry(i + 1, wStart, wEnd, buckets[i]));
            }

            // MostActiveDay: single calendar day with most step completions
            DateTime? mostActiveDay = null;
            if (totalStepsCompleted > 0)
            {
                var maxDay = stepCountByDay.MaxBy(kv => kv.Value).Key;
                mostActiveDay = maxDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            }

            // MostProductiveWeek: 1-based week number with most steps (null if no steps)
            int? mostProductiveWeek = null;
            if (totalStepsCompleted > 0)
            {
                var maxBucket = 0;
                for (int i = 1; i < BucketCount; i++)
                    if (buckets[i] > buckets[maxBucket]) maxBucket = i;
                mostProductiveWeek = maxBucket + 1;
            }

            var averageStepsPerDay = Math.Round(totalStepsCompleted / (double)TotalDays, 2);

            _logger.LogInformation(
                "Monthly report for user {UserId}: {TotalSteps} steps completed, {GoalsProgressed} goals progressed",
                userId, totalStepsCompleted, totalGoalsProgressed);

            return new MonthlyReportResponse(
                monthStartDt,
                monthEndDt,
                totalStepsCompleted,
                totalGoalsProgressed,
                weeklyBreakdown,
                mostActiveDay,
                mostProductiveWeek,
                averageStepsPerDay);
        }
    }
}
