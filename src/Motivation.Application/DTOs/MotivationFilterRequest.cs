namespace Motivation.Application.DTOs
{
    public record MotivationFilterRequest : PagedRequest
    {
        public string? Search { get; init; }
        public string SortBy { get; init; }
        public string SortOrder { get; init; }

        public MotivationFilterRequest(int page = 1, int pageSize = 10, string? search = null, string? sortBy = null, string? sortOrder = null)
            : base(page, pageSize)
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            SortBy = sortBy?.ToLowerInvariant() ?? "createdat";
            SortOrder = sortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        }
    }
}
