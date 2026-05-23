using System;
using System.Collections.Generic;
using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record CreateStepResponse(Guid Id, Guid GoalId, string Title, bool IsCompleted, DateTime? CompletedAt, string? Notes = null, DateTime? DueDate = null, bool IsOverdue = false, StepPriority Priority = StepPriority.None, int Order = 0, IReadOnlyList<string>? Tags = null);
}
