using System;
using System.Collections.Generic;
using System.Linq;

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
        public bool IsArchived { get; private set; }
        public bool IsPinned { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public string TagsRaw { get; private set; } = string.Empty;

        public IReadOnlyList<string> Tags =>
            string.IsNullOrWhiteSpace(TagsRaw)
                ? Array.Empty<string>()
                : TagsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        protected Goal() { }

        public Goal(Guid id, Guid userId, string title, string description, GoalStatus status, DateTime createdAt, DateTime? deadline = null, GoalPriority priority = GoalPriority.None, string? notes = null, bool isArchived = false, bool isPinned = false, DateTime? completedAt = null, string? tagsRaw = null)
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
            IsArchived = isArchived;
            IsPinned = isPinned;
            CompletedAt = completedAt;
            TagsRaw = tagsRaw ?? string.Empty;
        }

        public void UpdateStatus(GoalStatus newStatus)
        {
            if (newStatus == GoalStatus.Completed && Status != GoalStatus.Completed)
                CompletedAt = DateTime.UtcNow;
            else if (newStatus != GoalStatus.Completed)
                CompletedAt = null;
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

        public void SetTags(IEnumerable<string>? tags)
        {
            if (tags == null)
            {
                TagsRaw = string.Empty;
                return;
            }
            TagsRaw = string.Join(',', tags.Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        public void Archive() => IsArchived = true;

        public void Unarchive() => IsArchived = false;

        public void Pin() => IsPinned = true;

        public void Unpin() => IsPinned = false;

        public bool IsOverdue(DateTime now) =>
            Deadline.HasValue
            && Deadline.Value < now
            && Status != GoalStatus.Completed
            && Status != GoalStatus.Cancelled;

        public void Update(string? title, string? description, GoalStatus? status, DateTime? deadline = null, bool clearDeadline = false, GoalPriority? priority = null, string? notes = null, bool clearNotes = false, IEnumerable<string>? tags = null, bool clearTags = false)
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
            if (clearTags)
                SetTags(null);
            else if (tags != null)
                SetTags(tags);
        }
    }
}