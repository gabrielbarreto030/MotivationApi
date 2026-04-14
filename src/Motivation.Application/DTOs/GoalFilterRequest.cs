using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record GoalFilterRequest : PagedRequest
    {
        public GoalStatus? Status { get; init; }

        public GoalFilterRequest(int page = 1, int pageSize = 10, GoalStatus? status = null)
            : base(page, pageSize)
        {
            Status = status;
        }
    }
}
