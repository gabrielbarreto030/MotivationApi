using System.Collections.Generic;

namespace Motivation.Application.DTOs
{
    public record MotivationStatsResponse(
        int TotalMotivations,
        int TotalFavorites,
        int RatedMotivations,
        double? AverageRating,
        Dictionary<string, int> TagBreakdown
    );
}
