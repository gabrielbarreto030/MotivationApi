namespace Motivation.Application.DTOs
{
    public record UpdateStepRequest(string? Title = null, string? Notes = null, bool ClearNotes = false);
}
