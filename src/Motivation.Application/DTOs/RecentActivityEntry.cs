using System;

namespace Motivation.Application.DTOs
{
    public record RecentActivityEntry(
        Guid StepId,
        string StepTitle,
        Guid GoalId,
        string GoalTitle,
        DateTime CompletedAt
    );
}
