namespace Motivation.Application.DTOs
{
    public record GoalProgressResponse(
        Guid GoalId,
        int TotalSteps,
        int CompletedSteps,
        double ProgressPercentage
    );
}
