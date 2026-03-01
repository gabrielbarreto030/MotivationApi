using System;

namespace Motivation.Domain.Entities
{
    public class Motivation
    {
        public Guid Id { get; private set; }
        public Guid GoalId { get; private set; }
        public string Text { get; private set; }

        protected Motivation() { }

        public Motivation(Guid id, Guid goalId, string text)
        {
            if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty", nameof(id));
            if (goalId == Guid.Empty) throw new ArgumentException("GoalId cannot be empty", nameof(goalId));
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text is required", nameof(text));

            Id = id;
            GoalId = goalId;
            Text = text;
        }
    }
}