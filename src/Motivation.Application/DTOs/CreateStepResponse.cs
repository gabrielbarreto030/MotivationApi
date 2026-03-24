using System;

namespace Motivation.Application.DTOs
{
    public record CreateStepResponse(Guid Id, Guid GoalId, string Title, bool IsCompleted, DateTime? CompletedAt);
}
