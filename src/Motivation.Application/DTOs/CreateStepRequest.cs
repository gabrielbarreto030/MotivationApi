namespace Motivation.Application.DTOs
{
    public record CreateStepRequest(string Title, string? Notes = null);
}
