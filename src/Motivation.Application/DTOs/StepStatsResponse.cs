using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record StepStatsResponse(
        int TotalSteps,
        int CompletedSteps,
        int PendingSteps,
        int OverdueSteps,
        double CompletionPercentage,
        Dictionary<string, int> PriorityBreakdown,
        Dictionary<string, int> TagBreakdown
    );
}
