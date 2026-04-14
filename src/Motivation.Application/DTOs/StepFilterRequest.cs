namespace Motivation.Application.DTOs
{
    public record StepFilterRequest : PagedRequest
    {
        public bool? IsCompleted { get; init; }
        public string SortBy { get; init; }
        public string SortOrder { get; init; }

        public StepFilterRequest(int page = 1, int pageSize = 10, bool? isCompleted = null, string? sortBy = null, string? sortOrder = null)
            : base(page, pageSize)
        {
            IsCompleted = isCompleted;
            SortBy = sortBy?.ToLowerInvariant() ?? "title";
            SortOrder = sortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        }
    }
}
