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
        public DateTime? DueDate { get; private set; }

        protected Step() { }

        public Step(Guid id, Guid goalId, string title, string? notes = null, DateTime? dueDate = null)
        {
            if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty", nameof(id));
            if (goalId == Guid.Empty) throw new ArgumentException("GoalId cannot be empty", nameof(goalId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required", nameof(title));

            Id = id;
            GoalId = goalId;
            Title = title;
            IsCompleted = false;
            Notes = notes;
            DueDate = dueDate;
        }

        public void MarkCompleted(DateTime completedAt)
        {
            IsCompleted = true;
            CompletedAt = completedAt;
        }

        public void UpdateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required", nameof(title));
            Title = title;
        }

        public void UpdateNotes(string? notes)
        {
            Notes = notes;
        }

        public void UpdateDueDate(DateTime? dueDate)
        {
            DueDate = dueDate;
        }

        public bool IsOverdue(DateTime now) =>
            DueDate.HasValue
            && DueDate.Value < now
            && !IsCompleted;
    }
}