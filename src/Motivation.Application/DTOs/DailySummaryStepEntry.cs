using System;

namespace Motivation.Application.DTOs
{
    public record DailySummaryStepEntry(
        Guid StepId,
        string StepTitle,
        DateTime CompletedAt
    );
}
