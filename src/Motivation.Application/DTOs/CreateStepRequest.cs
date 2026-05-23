using System;
using System.Collections.Generic;
using Motivation.Domain.Entities;

namespace Motivation.Application.DTOs
{
    public record CreateStepRequest(string Title, string? Notes = null, DateTime? DueDate = null, StepPriority Priority = StepPriority.None, IEnumerable<string>? Tags = null);
}
