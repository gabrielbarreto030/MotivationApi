using System;

namespace Motivation.Application.DTOs
{
    public record MonthlyActivityEntry(int MonthNumber, DateOnly MonthStart, DateOnly MonthEnd, int StepsCompleted);
}
