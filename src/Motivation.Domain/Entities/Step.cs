using System;

namespace Motivation.Domain.Entities
{
    public class Step
    {
        public Guid Id { get; private set; }
        public Guid GoalId { get; private set; }
        public string Title { get; private set; }
        public bool IsCompleted { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public string? Notes { get; private set; }

        protected Step() { }

        public Step(Guid id, Guid goalId, string title, string? notes = null)
        {
            if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty", nameof(id));
            if (goalId == Guid.Empty) throw new ArgumentException("GoalId cannot be empty", nameof(goalId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required", nameof(title));

            Id = id;
            GoalId = goalId;
            Title = title;
            IsCompleted = false;
            Notes = notes;
        }

        public void MarkCompleted(DateTime completedAt)
        {
            IsCompleted = true;
            CompletedAt = completedAt;
        }

        public void UpdateNotes(string? notes)
        {
            Notes = notes;
        }
    }
}