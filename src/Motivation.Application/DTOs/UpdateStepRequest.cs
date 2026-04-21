namespace Motivation.Application.DTOs
{
    public record UpdateStepRequest(string? Notes, bool ClearNotes = false);
}
