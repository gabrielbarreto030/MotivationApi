using System;
using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record YearlyReportResponse(
        DateTime YearStart,
        DateTime YearEnd,
        int TotalStepsCompleted,
        int TotalGoalsProgressed,
        IReadOnlyList<MonthlyActivityEntry> MonthlyBreakdown,
        DateTime? MostActiveDay,
        int? MostProductiveMonth,
        double AverageStepsPerDay
    );
}
