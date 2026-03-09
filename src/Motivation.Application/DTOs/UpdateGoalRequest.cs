namespace Motivation.Application.DTOs
{
    public record UpdateGoalRequest(string? Title, string? Description, string? Status);
}
