using System;
using FluentAssertions;
using Motivation.Domain.Entities;
using Xunit;

namespace Motivation.UnitTests.DomainLayer
{
    public class StepEntityTests
    {
        private static readonly Guid ValidId = Guid.NewGuid();
        private static readonly Guid ValidGoalId = Guid.NewGuid();
        private const string ValidTitle = "Complete chapter 1";

        private Step CreateValidStep(
            Guid? id = null,
            Guid? goalId = null,
            string title = ValidTitle)
        {
            return new Step(id ?? ValidId, goalId ?? ValidGoalId, title);
        }

        // ── Constructor ──────────────────────────────────────────────────────────

        [Fact]
        public void Constructor_WithValidData_ShouldCreateStep()
        {
            var step = CreateValidStep();

            step.Id.Should().Be(ValidId);
            step.GoalId.Should().Be(ValidGoalId);
            step.Title.Should().Be(ValidTitle);
        }

        [Fact]
        public void Constructor_NewStep_ShouldNotBeCompleted()
        {
            var step = CreateValidStep();

            step.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void Constructor_NewStep_ShouldHaveNullCompletedAt()
        {
            var step = CreateValidStep();

            step.CompletedAt.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithEmptyId_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidStep(id: Guid.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("id");
        }

        [Fact]
        public void Constructor_WithEmptyGoalId_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidStep(goalId: Guid.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("goalId");
        }

        [Fact]
        public void Constructor_WithNullTitle_ShouldThrowArgumentException()
        {
            Action act = () => new Step(ValidId, ValidGoalId, null!);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("title");
        }

        [Fact]
        public void Constructor_WithEmptyTitle_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidStep(title: string.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("title");
        }

        [Fact]
        public void Constructor_WithWhiteSpaceTitle_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidStep(title: "   ");

            act.Should().Throw<ArgumentException>()
               .WithParameterName("title");
        }

        // ── MarkCompleted ────────────────────────────────────────────────────────

        [Fact]
        public void MarkCompleted_ShouldSetIsCompletedTrue()
        {
            var step = CreateValidStep();

            step.MarkCompleted(DateTime.UtcNow);

            step.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public void MarkCompleted_ShouldSetCompletedAt()
        {
            var step = CreateValidStep();
            var completedAt = new DateTime(2024, 3, 20, 14, 30, 0, DateTimeKind.Utc);

            step.MarkCompleted(completedAt);

            step.CompletedAt.Should().Be(completedAt);
        }

        [Fact]
        public void MarkCompleted_ShouldNotChangeOtherProperties()
        {
            var step = CreateValidStep();
            var originalTitle = step.Title;
            var originalGoalId = step.GoalId;

            step.MarkCompleted(DateTime.UtcNow);

            step.Title.Should().Be(originalTitle);
            step.GoalId.Should().Be(originalGoalId);
        }

        [Fact]
        public void MarkCompleted_CalledTwice_ShouldOverwriteCompletedAt()
        {
            var step = CreateValidStep();
            var firstDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var secondDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

            step.MarkCompleted(firstDate);
            step.MarkCompleted(secondDate);

            step.IsCompleted.Should().BeTrue();
            step.CompletedAt.Should().Be(secondDate);
        }

        [Fact]
        public void TwoIndependentSteps_MarkCompletingOne_ShouldNotAffectOther()
        {
            var step1 = new Step(Guid.NewGuid(), ValidGoalId, "Step 1");
            var step2 = new Step(Guid.NewGuid(), ValidGoalId, "Step 2");

            step1.MarkCompleted(DateTime.UtcNow);

            step1.IsCompleted.Should().BeTrue();
            step2.IsCompleted.Should().BeFalse();
            step2.CompletedAt.Should().BeNull();
        }
    }
}
