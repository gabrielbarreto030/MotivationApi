using System;
using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record DailySummaryResponse(
        DateOnly Date,
        int TotalStepsCompleted,
        int GoalsProgressed,
        IReadOnlyList<DailySummaryGoalEntry> Entries
    );
}
