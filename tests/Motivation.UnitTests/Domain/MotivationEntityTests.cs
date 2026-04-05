using System;
using FluentAssertions;
using Xunit;

namespace Motivation.UnitTests.DomainLayer
{
    public class MotivationEntityTests
    {
        private static readonly Guid ValidId = Guid.NewGuid();
        private static readonly Guid ValidGoalId = Guid.NewGuid();
        private const string ValidText = "Every day is a new opportunity to grow!";

        private Motivation.Domain.Entities.Motivation CreateValidMotivation(
            Guid? id = null,
            Guid? goalId = null,
            string text = ValidText)
        {
            return new Motivation.Domain.Entities.Motivation(
                id ?? ValidId,
                goalId ?? ValidGoalId,
                text);
        }

        // ── Constructor ──────────────────────────────────────────────────────────

        [Fact]
        public void Constructor_WithValidData_ShouldCreateMotivation()
        {
            var motivation = CreateValidMotivation();

            motivation.Id.Should().Be(ValidId);
            motivation.GoalId.Should().Be(ValidGoalId);
            motivation.Text.Should().Be(ValidText);
        }

        [Fact]
        public void Constructor_WithEmptyId_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidMotivation(id: Guid.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("id");
        }

        [Fact]
        public void Constructor_WithEmptyGoalId_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidMotivation(goalId: Guid.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("goalId");
        }

        [Fact]
        public void Constructor_WithNullText_ShouldThrowArgumentException()
        {
            Action act = () => new Motivation.Domain.Entities.Motivation(ValidId, ValidGoalId, null!);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("text");
        }

        [Fact]
        public void Constructor_WithEmptyText_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidMotivation(text: string.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("text");
        }

        [Fact]
        public void Constructor_WithWhiteSpaceText_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidMotivation(text: "   ");

            act.Should().Throw<ArgumentException>()
               .WithParameterName("text");
        }

        [Fact]
        public void Constructor_WithLongText_ShouldSucceed()
        {
            var longText = new string('x', 500);

            var motivation = CreateValidMotivation(text: longText);

            motivation.Text.Should().Be(longText);
            motivation.Text.Length.Should().Be(500);
        }

        [Fact]
        public void Constructor_TwoDifferentMotivations_ShouldBeIndependent()
        {
            var m1 = new Motivation.Domain.Entities.Motivation(Guid.NewGuid(), Guid.NewGuid(), "Text 1");
            var m2 = new Motivation.Domain.Entities.Motivation(Guid.NewGuid(), Guid.NewGuid(), "Text 2");

            m1.Id.Should().NotBe(m2.Id);
            m1.GoalId.Should().NotBe(m2.GoalId);
            m1.Text.Should().NotBe(m2.Text);
        }

        [Fact]
        public void Constructor_SameGoalId_TwoMotivations_ShouldBothBelongToSameGoal()
        {
            var sharedGoalId = Guid.NewGuid();

            var m1 = new Motivation.Domain.Entities.Motivation(Guid.NewGuid(), sharedGoalId, "Motivation 1");
            var m2 = new Motivation.Domain.Entities.Motivation(Guid.NewGuid(), sharedGoalId, "Motivation 2");

            m1.GoalId.Should().Be(sharedGoalId);
            m2.GoalId.Should().Be(sharedGoalId);
            m1.Id.Should().NotBe(m2.Id);
        }
    }
}
