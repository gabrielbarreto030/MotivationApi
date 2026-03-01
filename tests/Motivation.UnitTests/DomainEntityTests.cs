using System;
using FluentAssertions;
using Motivation.Domain.Entities;
using Xunit;

namespace Motivation.UnitTests
{
    public class DomainEntityTests
    {
        [Fact]
        public void CreateUser_WithValidData_ShouldSucceed()
        {
            var id = Guid.NewGuid();
            var email = "test@example.com";
            var hash = "hashed";
            var created = DateTime.UtcNow;

            var user = new User(id, email, hash, created);

            user.Id.Should().Be(id);
            user.Email.Should().Be(email);
            user.PasswordHash.Should().Be(hash);
            user.CreatedAt.Should().Be(created);
        }

        [Fact]
        public void CreateUser_WithEmptyEmail_ShouldThrow()
        {
            Action act = () => new User(Guid.NewGuid(), "", "hash", DateTime.UtcNow);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Goal_CreationAndStatusUpdate_Works()
        {
            var id = Guid.NewGuid();
            var uid = Guid.NewGuid();
            var goal = new Goal(id, uid, "Title", "Desc", GoalStatus.Pending, DateTime.UtcNow);

            goal.Status.Should().Be(GoalStatus.Pending);
            goal.UpdateStatus(GoalStatus.Completed);
            goal.Status.Should().Be(GoalStatus.Completed);
        }

        [Fact]
        public void Step_MarkCompleted_SetsProperties()
        {
            var step = new Step(Guid.NewGuid(), Guid.NewGuid(), "Step1");
            step.IsCompleted.Should().BeFalse();

            var now = DateTime.UtcNow;
            step.MarkCompleted(now);

            step.IsCompleted.Should().BeTrue();
            step.CompletedAt.Should().Be(now);
        }

        [Fact]
        public void Motivation_Creation_RequiresText()
        {
            Action actBad = () => new Motivation.Domain.Entities.Motivation(Guid.NewGuid(), Guid.NewGuid(), "");
            actBad.Should().Throw<ArgumentException>();

            var mot = new Motivation.Domain.Entities.Motivation(Guid.NewGuid(), Guid.NewGuid(), "Keep going");
            mot.Text.Should().Be("Keep going");
        }
    }
}
