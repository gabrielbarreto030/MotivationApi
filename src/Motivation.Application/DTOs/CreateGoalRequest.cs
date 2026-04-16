using System;

namespace Motivation.Application.DTOs
{
    public record CreateGoalRequest(string Title, string Description, DateTime? Deadline = null);
}