namespace Motivation.Application.DTOs
{
    public record MotivationFilterRequest : PagedRequest
    {
        public string? Search { get; init; }

        public MotivationFilterRequest(int page = 1, int pageSize = 10, string? search = null)
            : base(page, pageSize)
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        }
    }
}
