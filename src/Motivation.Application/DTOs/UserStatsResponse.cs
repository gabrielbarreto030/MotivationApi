namespace Motivation.Application.DTOs
{
    public record UserStatsResponse(
        int TotalGoals,
        int PinnedGoals,
        int ArchivedGoals,
        int OverdueGoals,
        int GoalsPending,
        int GoalsInProgress,
        int GoalsCompleted,
        int GoalsCancelled,
        int TotalSteps,
        int CompletedSteps,
        int PendingSteps,
        int OverdueSteps,
        int TotalMotivations
    );
}
