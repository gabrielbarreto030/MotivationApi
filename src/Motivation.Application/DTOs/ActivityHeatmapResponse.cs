using System;
using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record ActivityHeatmapResponse(
        DateTime WindowStart,
        DateTime WindowEnd,
        int TotalStepsCompleted,
        int ActiveDays,
        IReadOnlyList<HeatmapEntry> Entries
    );
}
