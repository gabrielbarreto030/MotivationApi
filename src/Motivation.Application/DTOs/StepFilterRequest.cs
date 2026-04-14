namespace Motivation.Application.DTOs
{
    public record StepFilterRequest : PagedRequest
    {
        public bool? IsCompleted { get; init; }

        public StepFilterRequest(int page = 1, int pageSize = 10, bool? isCompleted = null)
            : base(page, pageSize)
        {
            IsCompleted = isCompleted;
        }
    }
}
