using System;

namespace Motivation.Application.DTOs
{
    public record UpdateGoalRequest(string? Title, string? Description, string? Status, DateTime? Deadline = null, bool ClearDeadline = false);
}
