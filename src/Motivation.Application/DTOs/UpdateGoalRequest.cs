using System;
using System.Collections.Generic;
using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record UpdateGoalRequest(string? Title, string? Description, string? Status, DateTime? Deadline = null, bool ClearDeadline = false, GoalPriority? Priority = null, string? Notes = null, bool ClearNotes = false, IEnumerable<string>? Tags = null, bool ClearTags = false);
}
