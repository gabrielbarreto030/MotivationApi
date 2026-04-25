using System;

namespace Motivation.Application.DTOs
{
    public record UpdateStepRequest(string? Title = null, string? Notes = null, bool ClearNotes = false, DateTime? DueDate = null, bool ClearDueDate = false);
}
