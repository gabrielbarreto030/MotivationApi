using System;
using FluentAssertions;
using Motivation.Domain.Entities;
using Xunit;

namespace Motivation.UnitTests.DomainLayer
{
    public class UserEntityTests
    {
        private static readonly Guid ValidId = Guid.NewGuid();
        private const string ValidEmail = "user@example.com";
        private const string ValidHash = "hashed_password_123";
        private static readonly DateTime ValidDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Constructor_WithValidData_ShouldCreateUser()
        {
            var user = new User(ValidId, ValidEmail, ValidHash, ValidDate);

            user.Id.Should().Be(ValidId);
            user.Email.Should().Be(ValidEmail);
            user.PasswordHash.Should().Be(ValidHash);
            user.CreatedAt.Should().Be(ValidDate);
        }

        [Fact]
        public void Constructor_WithEmptyId_ShouldThrowArgumentException()
        {
            Action act = () => new User(Guid.Empty, ValidEmail, ValidHash, ValidDate);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("id");
        }

        [Fact]
        public void Constructor_WithNullEmail_ShouldThrowArgumentException()
        {
            Action act = () => new User(ValidId, null!, ValidHash, ValidDate);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("email");
        }

        [Fact]
        public void Constructor_WithEmptyEmail_ShouldThrowArgumentException()
        {
            Action act = () => new User(ValidId, string.Empty, ValidHash, ValidDate);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("email");
        }

        [Fact]
        public void Constructor_WithWhiteSpaceEmail_ShouldThrowArgumentException()
        {
            Action act = () => new User(ValidId, "   ", ValidHash, ValidDate);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("email");
        }

        [Fact]
        public void Constructor_WithNullPasswordHash_ShouldThrowArgumentException()
        {
            Action act = () => new User(ValidId, ValidEmail, null!, ValidDate);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("passwordHash");
        }

        [Fact]
        public void Constructor_WithEmptyPasswordHash_ShouldThrowArgumentException()
        {
            Action act = () => new User(ValidId, ValidEmail, string.Empty, ValidDate);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("passwordHash");
        }

        [Fact]
        public void Constructor_WithWhiteSpacePasswordHash_ShouldThrowArgumentException()
        {
            Action act = () => new User(ValidId, ValidEmail, "   ", ValidDate);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("passwordHash");
        }

        [Fact]
        public void Constructor_ShouldPreserveCreatedAtExactly()
        {
            var specificDate = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);

            var user = new User(ValidId, ValidEmail, ValidHash, specificDate);

            user.CreatedAt.Should().Be(specificDate);
        }

        [Fact]
        public void Constructor_TwoDifferentUsers_ShouldBeIndependent()
        {
            var user1 = new User(Guid.NewGuid(), "a@example.com", "hash1", DateTime.UtcNow);
            var user2 = new User(Guid.NewGuid(), "b@example.com", "hash2", DateTime.UtcNow);

            user1.Id.Should().NotBe(user2.Id);
            user1.Email.Should().NotBe(user2.Email);
            user1.PasswordHash.Should().NotBe(user2.PasswordHash);
        }
    }
}
