using System;
using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record GoalTimelineResponse(
        Guid GoalId,
        string GoalTitle,
        int TotalSteps,
        int CompletedSteps,
        IReadOnlyList<GoalTimelineEntry> Entries);
}
