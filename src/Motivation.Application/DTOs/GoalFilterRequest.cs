using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record GoalFilterRequest : PagedRequest
    {
        public GoalStatus? Status { get; init; }
        public string SortBy { get; init; }
        public string SortOrder { get; init; }

        public GoalFilterRequest(int page = 1, int pageSize = 10, GoalStatus? status = null, string? sortBy = null, string? sortOrder = null)
            : base(page, pageSize)
        {
            Status = status;
            SortBy = sortBy?.ToLowerInvariant() ?? "createdat";
            SortOrder = sortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        }
    }
}
