using System;
using System.Collections.Generic;
using System.Linq;

namespace Motivation.Domain.Entities
{
    public class Motivation
    {
        public Guid Id { get; private set; }
        public Guid GoalId { get; private set; }
        public string Text { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public string TagsRaw { get; private set; } = string.Empty;
        public bool IsFavorite { get; private set; }

        public IReadOnlyList<string> Tags =>
            string.IsNullOrWhiteSpace(TagsRaw)
                ? Array.Empty<string>()
                : TagsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        protected Motivation() { }

        public Motivation(Guid id, Guid goalId, string text, DateTime createdAt = default, string? tagsRaw = null, bool isFavorite = false)
        {
            if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty", nameof(id));
            if (goalId == Guid.Empty) throw new ArgumentException("GoalId cannot be empty", nameof(goalId));
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text is required", nameof(text));

            Id = id;
            GoalId = goalId;
            Text = text;
            CreatedAt = createdAt == default ? DateTime.UtcNow : createdAt;
            TagsRaw = tagsRaw ?? string.Empty;
            IsFavorite = isFavorite;
        }

        public void UpdateText(string newText)
        {
            if (string.IsNullOrWhiteSpace(newText)) throw new ArgumentException("Text is required", nameof(newText));
            Text = newText;
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

        public void Favorite()
        {
            if (IsFavorite) throw new InvalidOperationException("Motivation is already a favorite");
            IsFavorite = true;
        }

        public void Unfavorite()
        {
            if (!IsFavorite) throw new InvalidOperationException("Motivation is not a favorite");
            IsFavorite = false;
        }
    }
}