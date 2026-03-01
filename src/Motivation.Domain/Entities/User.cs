using System;

namespace Motivation.Domain.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string Email { get; private set; }
        public string PasswordHash { get; private set; }
        public DateTime CreatedAt { get; private set; }

        // parameterless constructor for EF and serialization
        protected User() { }

        public User(Guid id, string email, string passwordHash, DateTime createdAt)
        {
            if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty", nameof(id));
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required", nameof(email));
            if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("PasswordHash is required", nameof(passwordHash));

            Id = id;
            Email = email;
            PasswordHash = passwordHash;
            CreatedAt = createdAt;
        }
    }
}