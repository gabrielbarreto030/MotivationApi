using System;
using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record WeeklyReportResponse(
        DateTime WeekStart,
        DateTime WeekEnd,
        int TotalStepsCompleted,
        int TotalGoalsProgressed,
        IReadOnlyList<DailyActivityEntry> DailyBreakdown,
        DateTime? MostActiveDay,
        double AverageStepsPerDay
    );
}
