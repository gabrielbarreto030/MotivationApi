using System;

namespace Motivation.Application.DTOs
{
    public record WeeklyActivityEntry(int WeekNumber, DateOnly WeekStart, DateOnly WeekEnd, int StepsCompleted);
}
