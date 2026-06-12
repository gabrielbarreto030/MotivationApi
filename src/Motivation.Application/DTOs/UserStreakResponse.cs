using System;

namespace Motivation.Application.DTOs
{
    public record UserStreakResponse(
        int CurrentStreak,
        int LongestStreak,
        DateTime? LastActivityDate
    );
}
