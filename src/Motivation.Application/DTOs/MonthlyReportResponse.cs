using System;
using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record MonthlyReportResponse(
        DateTime MonthStart,
        DateTime MonthEnd,
        int TotalStepsCompleted,
        int TotalGoalsProgressed,
        IReadOnlyList<WeeklyActivityEntry> WeeklyBreakdown,
        DateTime? MostActiveDay,
        int? MostProductiveWeek,
        double AverageStepsPerDay
    );
}
