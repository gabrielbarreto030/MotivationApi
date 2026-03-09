using System;
using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record UpdateGoalResponse(Guid Id, string Title, string Description, GoalStatus Status, DateTime CreatedAt);
}
