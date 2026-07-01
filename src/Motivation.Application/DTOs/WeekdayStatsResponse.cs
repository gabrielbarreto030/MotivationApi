using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record WeekdayStatsResponse(
        int TotalStepsCompleted,
        string? MostProductiveDay,
        IReadOnlyList<WeekdayEntry> Entries);
}
