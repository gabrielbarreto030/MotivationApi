namespace Motivation.Application.DTOs
{
    public record UserGoalsSummaryResponse(
        int TotalGoals,
        int Pending,
        int InProgress,
        int Completed,
        int Cancelled,
        int TotalSteps,
        int CompletedSteps,
        double OverallCompletionRate
    );
}
