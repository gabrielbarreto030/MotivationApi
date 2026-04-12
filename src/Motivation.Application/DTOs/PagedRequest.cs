namespace Motivation.Application.DTOs
{
    public record PagedRequest
    {
        public int Page { get; init; }
        public int PageSize { get; init; }

        public PagedRequest(int page = 1, int pageSize = 10)
        {
            Page = page < 1 ? 1 : page;
            PageSize = pageSize < 1 ? 1 : pageSize > 50 ? 50 : pageSize;
        }
    }
}
