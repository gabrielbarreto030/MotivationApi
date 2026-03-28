using System;

namespace Motivation.Application.DTOs
{
    public record AddMotivationResponse(Guid Id, Guid GoalId, string Text);
}
