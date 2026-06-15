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
    public class YearlyReportService : IYearlyReportService
    {
        // The 365-day window is divided into 12 buckets.
        // BucketSize = 365 / 12 = 30 (integer division).
        // Buckets 1-11 span 30 days each; bucket 12 absorbs the remaining 35 days.
        private const int TotalDays = 365;
        private const int BucketCount = 12;
        private static readonly int BucketSize = TotalDays / BucketCount; // 30

        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<YearlyReportService> _logger;

        public YearlyReportService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<YearlyReportService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        public async Task<YearlyReportResponse> GetYearlyReportAsync(Guid userId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yearStart = today.AddDays(-(TotalDays - 1)); // 364 days ago

            var yearStartDt = yearStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var yearEndDt = today.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

            // 12 monthly buckets
            var buckets = new int[BucketCount];

            // Track daily step counts for MostActiveDay
            var stepCountByDay = new Dictionary<DateOnly, int>();
            for (var d = yearStart; d <= today; d = d.AddDays(1))
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
                    if (completedDate < yearStart || completedDate > today)
                        continue;

                    stepCountByDay[completedDate]++;
                    goalsProgressedSet.Add(goal.Id);

                    // Assign to bucket by days elapsed from yearStart
                    var daysFromStart = completedDate.DayNumber - yearStart.DayNumber;
                    var bucketIndex = daysFromStart / BucketSize;
                    // Clamp to last bucket (bucket 12 absorbs the remainder beyond 11*30 = 330 days)
                    if (bucketIndex >= BucketCount) bucketIndex = BucketCount - 1;
                    buckets[bucketIndex]++;
                }
            }

            var totalStepsCompleted = stepCountByDay.Values.Sum();
            var totalGoalsProgressed = goalsProgressedSet.Count;

            // Build MonthlyBreakdown (12 entries, MonthNumber 1 = oldest)
            var monthlyBreakdown = new List<MonthlyActivityEntry>(BucketCount);
            for (int i = 0; i < BucketCount; i++)
            {
                var mStart = yearStart.AddDays(i * BucketSize);
                var mEnd = (i < BucketCount - 1)
                    ? mStart.AddDays(BucketSize - 1)
                    : today; // last bucket ends today
                monthlyBreakdown.Add(new MonthlyActivityEntry(i + 1, mStart, mEnd, buckets[i]));
            }

            // MostActiveDay: single calendar day with most step completions
            DateTime? mostActiveDay = null;
            if (totalStepsCompleted > 0)
            {
                var maxDay = stepCountByDay.MaxBy(kv => kv.Value).Key;
                mostActiveDay = maxDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            }

            // MostProductiveMonth: 1-based month number with most steps (null if no steps)
            int? mostProductiveMonth = null;
            if (totalStepsCompleted > 0)
            {
                var maxBucket = 0;
                for (int i = 1; i < BucketCount; i++)
                    if (buckets[i] > buckets[maxBucket]) maxBucket = i;
                mostProductiveMonth = maxBucket + 1;
            }

            var averageStepsPerDay = Math.Round(totalStepsCompleted / (double)TotalDays, 2);

            _logger.LogInformation(
                "Yearly report for user {UserId}: {TotalSteps} steps completed, {GoalsProgressed} goals progressed",
                userId, totalStepsCompleted, totalGoalsProgressed);

            return new YearlyReportResponse(
                yearStartDt,
                yearEndDt,
                totalStepsCompleted,
                totalGoalsProgressed,
                monthlyBreakdown,
                mostActiveDay,
                mostProductiveMonth,
                averageStepsPerDay);
        }
    }
}
