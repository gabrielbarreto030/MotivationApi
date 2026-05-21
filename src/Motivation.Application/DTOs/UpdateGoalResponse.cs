using System;
using System.Collections.Generic;
using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record UpdateGoalResponse(Guid Id, string Title, string Description, GoalStatus Status, GoalPriority Priority, DateTime CreatedAt, DateTime? Deadline, bool IsOverdue, string? Notes, bool IsArchived, bool IsPinned, DateTime? CompletedAt, IReadOnlyList<string> Tags);
}
