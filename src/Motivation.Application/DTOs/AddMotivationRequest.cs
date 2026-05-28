using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record AddMotivationRequest(string Text, IEnumerable<string>? Tags = null);
}
