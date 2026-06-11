using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record GoalStatsResponse(
        int TotalGoals,
        Dictionary<string, int> GoalsByStatus,
        Dictionary<string, int> GoalsByPriority,
        int ArchivedGoals,
        int PinnedGoals,
        int OverdueGoals,
        Dictionary<string, int> TagBreakdown,
        double? AvgCompletionDays
    );
}
