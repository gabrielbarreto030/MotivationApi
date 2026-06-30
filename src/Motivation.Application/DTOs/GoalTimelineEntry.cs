using System;

namespace Motivation.Application.DTOs
{
    public record GoalTimelineEntry(Guid StepId, string StepTitle, DateTime CompletedAt);
}
