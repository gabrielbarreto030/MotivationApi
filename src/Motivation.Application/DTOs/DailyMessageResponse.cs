using System;

namespace Motivation.Application.DTOs
{
    public record DailyMessageResponse(string Message, DateOnly Date);
}
