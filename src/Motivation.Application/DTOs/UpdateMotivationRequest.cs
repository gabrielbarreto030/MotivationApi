using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record UpdateMotivationRequest(string Text, IEnumerable<string>? Tags = null, bool ClearTags = false);
}
