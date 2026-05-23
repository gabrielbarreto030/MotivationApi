using System;
using System.Collections.Generic;
using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record UpdateStepRequest(string? Title = null, string? Notes = null, bool ClearNotes = false, DateTime? DueDate = null, bool ClearDueDate = false, StepPriority? Priority = null, IEnumerable<string>? Tags = null, bool ClearTags = false);
}
