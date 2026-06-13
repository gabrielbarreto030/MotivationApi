using System;

namespace Motivation.Application.DTOs
{
    public record DailyActivityEntry(DateOnly Date, int StepsCompleted);
}
