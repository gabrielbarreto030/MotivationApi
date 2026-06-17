using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record RecentActivityResponse(
        int TotalCount,
        int Page,
        int PageSize,
        IReadOnlyList<RecentActivityEntry> Entries
    );
}
