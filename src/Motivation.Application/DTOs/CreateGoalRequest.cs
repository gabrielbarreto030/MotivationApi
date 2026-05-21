using System;
using System.Collections.Generic;
using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record CreateGoalRequest(string Title, string Description, DateTime? Deadline = null, GoalPriority Priority = GoalPriority.None, string? Notes = null, IEnumerable<string>? Tags = null);
}