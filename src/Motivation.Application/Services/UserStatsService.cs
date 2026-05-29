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
    public class UserStatsService : IUserStatsService
    {
        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly IMotivationRepository _motivationRepository;
        private readonly ILogger<UserStatsService> _logger;

        public UserStatsService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            IMotivationRepository motivationRepository,
            ILogger<UserStatsService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _motivationRepository = motivationRepository;
            _logger = logger;
        }

        public async Task<UserStatsResponse> GetStatsAsync(Guid userId)
        {
            var goals = await _goalRepository.GetByUserAsync(userId);
            var now = DateTime.UtcNow;

            var totalGoals = goals.Length;
            var pinnedGoals = goals.Count(g => g.IsPinned);
            var archivedGoals = goals.Count(g => g.IsArchived);
            var overdueGoals = goals.Count(g => g.IsOverdue(now));
            var goalsPending = goals.Count(g => g.Status == GoalStatus.Pending);
            var goalsInProgress = goals.Count(g => g.Status == GoalStatus.InProgress);
            var goalsCompleted = goals.Count(g => g.Status == GoalStatus.Completed);
            var goalsCancelled = goals.Count(g => g.Status == GoalStatus.Cancelled);

            var totalSteps = 0;
            var completedSteps = 0;
            var overdueSteps = 0;
            var totalMotivations = 0;

            foreach (var goal in goals)
            {
                var steps = await _stepRepository.GetByGoalAsync(goal.Id);
                totalSteps += steps.Length;
                completedSteps += steps.Count(s => s.IsCompleted);
                overdueSteps += steps.Count(s => s.IsOverdue(now));

                var motivations = await _motivationRepository.GetByGoalAsync(goal.Id);
                totalMotivations += motivations.Length;
            }

            var pendingSteps = totalSteps - completedSteps;

            _logger.LogInformation(
                "Stats computed for user {UserId}: {TotalGoals} goals, {TotalSteps} steps, {TotalMotivations} motivations",
                userId, totalGoals, totalSteps, totalMotivations);

            return new UserStatsResponse(
                TotalGoals: totalGoals,
                PinnedGoals: pinnedGoals,
                ArchivedGoals: archivedGoals,
                OverdueGoals: overdueGoals,
                GoalsPending: goalsPending,
                GoalsInProgress: goalsInProgress,
                GoalsCompleted: goalsCompleted,
                GoalsCancelled: goalsCancelled,
                TotalSteps: totalSteps,
                CompletedSteps: completedSteps,
                PendingSteps: pendingSteps,
                OverdueSteps: overdueSteps,
                TotalMotivations: totalMotivations
            );
        }
    }
}
