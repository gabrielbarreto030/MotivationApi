using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record StepFilterRequest : PagedRequest
    {
        public bool? IsCompleted { get; init; }
        public StepPriority? Priority { get; init; }
        public string SortBy { get; init; }
        public string SortOrder { get; init; }

        public StepFilterRequest(int page = 1, int pageSize = 10, bool? isCompleted = null, string? sortBy = null, string? sortOrder = null, StepPriority? priority = null)
            : base(page, pageSize)
        {
            IsCompleted = isCompleted;
            Priority = priority;
            SortBy = sortBy?.ToLowerInvariant() ?? "title";
            SortOrder = sortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        }
    }
}
