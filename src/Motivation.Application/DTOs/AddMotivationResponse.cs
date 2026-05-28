using System;
using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record AddMotivationResponse(Guid Id, Guid GoalId, string Text, DateTime CreatedAt, IReadOnlyList<string> Tags);
}
