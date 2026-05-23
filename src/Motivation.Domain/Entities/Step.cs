using System;
using System.Collections.Generic;
using System.Linq;

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
        public StepPriority Priority { get; private set; }
        public int Order { get; private set; }
        public string TagsRaw { get; private set; } = string.Empty;

        public IReadOnlyList<string> Tags =>
            string.IsNullOrWhiteSpace(TagsRaw)
                ? Array.Empty<string>()
                : TagsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        protected Step() { }

        public Step(Guid id, Guid goalId, string title, string? notes = null, DateTime? dueDate = null, StepPriority priority = StepPriority.None, int order = 0, string? tagsRaw = null)
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
            Priority = priority;
            Order = order;
            TagsRaw = tagsRaw ?? string.Empty;
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

        public void MarkCompleted(DateTime completedAt)
        {
            IsCompleted = true;
            CompletedAt = completedAt;
        }

        public void Uncomplete()
        {
            IsCompleted = false;
            CompletedAt = null;
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

        public void UpdatePriority(StepPriority priority)
        {
            Priority = priority;
        }

        public void UpdateOrder(int order)
        {
            if (order < 1) throw new ArgumentException("Order must be greater than zero", nameof(order));
            Order = order;
        }

        public bool IsOverdue(DateTime now) =>
            DueDate.HasValue
            && DueDate.Value < now
            && !IsCompleted;
    }
}