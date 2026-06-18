using System;
using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record DailySummaryGoalEntry(
        Guid GoalId,
        string GoalTitle,
        IReadOnlyList<DailySummaryStepEntry> Steps
    );
}
