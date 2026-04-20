using System;
using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record UpdateGoalResponse(Guid Id, string Title, string Description, GoalStatus Status, GoalPriority Priority, DateTime CreatedAt, DateTime? Deadline, bool IsOverdue, string? Notes);
}
