using System;
using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record CreateGoalResponse(Guid Id, string Title, string Description, GoalStatus Status, DateTime CreatedAt);
}