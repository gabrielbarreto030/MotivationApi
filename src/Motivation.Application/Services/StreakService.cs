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
    public class StreakService : IStreakService
    {
        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<StreakService> _logger;

        public StreakService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<StreakService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        public async Task<UserStreakResponse> GetStreakAsync(Guid userId)
        {
            var goals = await _goalRepository.GetByUserAsync(userId);

            var completedDates = new List<DateOnly>();

            foreach (var goal in goals)
            {
                var steps = await _stepRepository.GetByGoalAsync(goal.Id);
                foreach (var step in steps)
                {
                    if (step.IsCompleted && step.CompletedAt.HasValue)
                        completedDates.Add(DateOnly.FromDateTime(step.CompletedAt.Value.ToUniversalTime()));
                }
            }

            if (completedDates.Count == 0)
            {
                _logger.LogInformation("Streak computed for user {UserId}: no activity", userId);
                return new UserStreakResponse(0, 0, null);
            }

            var distinctDatesDesc = completedDates.Distinct().OrderByDescending(d => d).ToList();
            var distinctDatesAsc = distinctDatesDesc.AsEnumerable().Reverse().ToList();

            var lastActivityDate = distinctDatesDesc[0].ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);

            // Current streak: consecutive days ending today or yesterday
            int currentStreak = 0;
            var mostRecent = distinctDatesDesc[0];
            if (mostRecent == today || mostRecent == yesterday)
            {
                var cursor = mostRecent;
                foreach (var date in distinctDatesDesc)
                {
                    if (date == cursor)
                    {
                        currentStreak++;
                        cursor = cursor.AddDays(-1);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Longest streak: max consecutive day run across all activity
            int longestStreak = 0;
            int runLength = 0;
            DateOnly? prev = null;
            foreach (var date in distinctDatesAsc)
            {
                if (prev == null)
                {
                    runLength = 1;
                }
                else if (date == prev.Value.AddDays(1))
                {
                    runLength++;
                }
                else
                {
                    longestStreak = Math.Max(longestStreak, runLength);
                    runLength = 1;
                }
                prev = date;
            }
            longestStreak = Math.Max(longestStreak, runLength);

            _logger.LogInformation(
                "Streak computed for user {UserId}: current={Current}, longest={Longest}",
                userId, currentStreak, longestStreak);

            return new UserStreakResponse(currentStreak, longestStreak, lastActivityDate);
        }
    }
}
