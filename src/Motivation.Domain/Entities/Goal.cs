using System;

namespace Motivation.Domain.Entities
{
    public class Goal
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public GoalStatus Status { get; private set; }
        public GoalPriority Priority { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? Deadline { get; private set; }
        public string? Notes { get; private set; }

        protected Goal() { }

        public Goal(Guid id, Guid userId, string title, string description, GoalStatus status, DateTime createdAt, DateTime? deadline = null, GoalPriority priority = GoalPriority.None, string? notes = null)
        {
            if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty", nameof(id));
            if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty", nameof(userId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required", nameof(title));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required", nameof(description));

            Id = id;
            UserId = userId;
            Title = title;
            Description = description;
            Status = status;
            Priority = priority;
            CreatedAt = createdAt;
            Deadline = deadline;
            Notes = notes;
        }

        public void UpdateStatus(GoalStatus newStatus)
        {
            Status = newStatus;
        }

        public void UpdateTitle(string newTitle)
        {
            if (string.IsNullOrWhiteSpace(newTitle))
                throw new ArgumentException("Title is required", nameof(newTitle));
            Title = newTitle;
        }

        public void UpdateDescription(string newDescription)
        {
            if (string.IsNullOrWhiteSpace(newDescription))
                throw new ArgumentException("Description is required", nameof(newDescription));
            Description = newDescription;
        }

        public void UpdateDeadline(DateTime? deadline)
        {
            Deadline = deadline;
        }

        public void UpdatePriority(GoalPriority priority)
        {
            Priority = priority;
        }

        public void UpdateNotes(string? notes)
        {
            Notes = notes;
        }

        public bool IsOverdue(DateTime now) =>
            Deadline.HasValue
            && Deadline.Value < now
            && Status != GoalStatus.Completed
            && Status != GoalStatus.Cancelled;

        public void Update(string? title, string? description, GoalStatus? status, DateTime? deadline = null, bool clearDeadline = false, GoalPriority? priority = null, string? notes = null, bool clearNotes = false)
        {
            if (!string.IsNullOrWhiteSpace(title))
                UpdateTitle(title);
            if (!string.IsNullOrWhiteSpace(description))
                UpdateDescription(description);
            if (status.HasValue)
                UpdateStatus(status.Value);
            if (clearDeadline)
                UpdateDeadline(null);
            else if (deadline.HasValue)
                UpdateDeadline(deadline.Value);
            if (priority.HasValue)
                UpdatePriority(priority.Value);
            if (clearNotes)
                UpdateNotes(null);
            else if (notes != null)
                UpdateNotes(notes);
        }
    }
}