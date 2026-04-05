using System;
using FluentAssertions;
using Motivation.Domain.Entities;
using Xunit;

namespace Motivation.UnitTests.DomainLayer
{
    public class GoalEntityTests
    {
        private static readonly Guid ValidId = Guid.NewGuid();
        private static readonly Guid ValidUserId = Guid.NewGuid();
        private const string ValidTitle = "Learn DDD";
        private const string ValidDescription = "Study Domain-Driven Design patterns";
        private static readonly DateTime ValidDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        private Goal CreateValidGoal(
            Guid? id = null,
            Guid? userId = null,
            string title = ValidTitle,
            string description = ValidDescription,
            GoalStatus status = GoalStatus.Pending,
            DateTime? createdAt = null)
        {
            return new Goal(
                id ?? ValidId,
                userId ?? ValidUserId,
                title,
                description,
                status,
                createdAt ?? ValidDate);
        }

        // ── Constructor ──────────────────────────────────────────────────────────

        [Fact]
        public void Constructor_WithValidData_ShouldCreateGoal()
        {
            var goal = CreateValidGoal();

            goal.Id.Should().Be(ValidId);
            goal.UserId.Should().Be(ValidUserId);
            goal.Title.Should().Be(ValidTitle);
            goal.Description.Should().Be(ValidDescription);
            goal.Status.Should().Be(GoalStatus.Pending);
            goal.CreatedAt.Should().Be(ValidDate);
        }

        [Fact]
        public void Constructor_WithEmptyId_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidGoal(id: Guid.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("id");
        }

        [Fact]
        public void Constructor_WithEmptyUserId_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidGoal(userId: Guid.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("userId");
        }

        [Fact]
        public void Constructor_WithNullTitle_ShouldThrowArgumentException()
        {
            Action act = () => new Goal(ValidId, ValidUserId, null!, ValidDescription, GoalStatus.Pending, ValidDate);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("title");
        }

        [Fact]
        public void Constructor_WithEmptyTitle_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidGoal(title: string.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("title");
        }

        [Fact]
        public void Constructor_WithWhiteSpaceTitle_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidGoal(title: "   ");

            act.Should().Throw<ArgumentException>()
               .WithParameterName("title");
        }

        [Fact]
        public void Constructor_WithNullDescription_ShouldThrowArgumentException()
        {
            Action act = () => new Goal(ValidId, ValidUserId, ValidTitle, null!, GoalStatus.Pending, ValidDate);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("description");
        }

        [Fact]
        public void Constructor_WithEmptyDescription_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidGoal(description: string.Empty);

            act.Should().Throw<ArgumentException>()
               .WithParameterName("description");
        }

        [Fact]
        public void Constructor_WithWhiteSpaceDescription_ShouldThrowArgumentException()
        {
            Action act = () => CreateValidGoal(description: "   ");

            act.Should().Throw<ArgumentException>()
               .WithParameterName("description");
        }

        [Theory]
        [InlineData(GoalStatus.Pending)]
        [InlineData(GoalStatus.InProgress)]
        [InlineData(GoalStatus.Completed)]
        [InlineData(GoalStatus.Cancelled)]
        public void Constructor_WithAnyStatus_ShouldSetStatus(GoalStatus status)
        {
            var goal = CreateValidGoal(status: status);

            goal.Status.Should().Be(status);
        }

        // ── UpdateTitle ──────────────────────────────────────────────────────────

        [Fact]
        public void UpdateTitle_WithValidTitle_ShouldUpdateTitle()
        {
            var goal = CreateValidGoal();
            var newTitle = "Updated Title";

            goal.UpdateTitle(newTitle);

            goal.Title.Should().Be(newTitle);
        }

        [Fact]
        public void UpdateTitle_WithEmptyTitle_ShouldThrowArgumentException()
        {
            var goal = CreateValidGoal();

            Action act = () => goal.UpdateTitle(string.Empty);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void UpdateTitle_WithWhiteSpaceTitle_ShouldThrowArgumentException()
        {
            var goal = CreateValidGoal();

            Action act = () => goal.UpdateTitle("   ");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void UpdateTitle_ShouldNotChangeOtherProperties()
        {
            var goal = CreateValidGoal();
            var originalDescription = goal.Description;
            var originalStatus = goal.Status;

            goal.UpdateTitle("New Title");

            goal.Description.Should().Be(originalDescription);
            goal.Status.Should().Be(originalStatus);
        }

        // ── UpdateDescription ────────────────────────────────────────────────────

        [Fact]
        public void UpdateDescription_WithValidDescription_ShouldUpdateDescription()
        {
            var goal = CreateValidGoal();
            var newDesc = "Updated description text";

            goal.UpdateDescription(newDesc);

            goal.Description.Should().Be(newDesc);
        }

        [Fact]
        public void UpdateDescription_WithEmptyDescription_ShouldThrowArgumentException()
        {
            var goal = CreateValidGoal();

            Action act = () => goal.UpdateDescription(string.Empty);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void UpdateDescription_WithWhiteSpaceDescription_ShouldThrowArgumentException()
        {
            var goal = CreateValidGoal();

            Action act = () => goal.UpdateDescription("   ");

            act.Should().Throw<ArgumentException>();
        }

        // ── UpdateStatus ─────────────────────────────────────────────────────────

        [Fact]
        public void UpdateStatus_ShouldChangeStatus()
        {
            var goal = CreateValidGoal(status: GoalStatus.Pending);

            goal.UpdateStatus(GoalStatus.InProgress);

            goal.Status.Should().Be(GoalStatus.InProgress);
        }

        [Fact]
        public void UpdateStatus_ToCompleted_ShouldWork()
        {
            var goal = CreateValidGoal(status: GoalStatus.InProgress);

            goal.UpdateStatus(GoalStatus.Completed);

            goal.Status.Should().Be(GoalStatus.Completed);
        }

        [Fact]
        public void UpdateStatus_ToCancelled_ShouldWork()
        {
            var goal = CreateValidGoal(status: GoalStatus.Pending);

            goal.UpdateStatus(GoalStatus.Cancelled);

            goal.Status.Should().Be(GoalStatus.Cancelled);
        }

        // ── Update (composite) ───────────────────────────────────────────────────

        [Fact]
        public void Update_WithAllValues_ShouldUpdateAll()
        {
            var goal = CreateValidGoal();
            var newTitle = "New Title";
            var newDesc = "New Description";
            var newStatus = GoalStatus.InProgress;

            goal.Update(newTitle, newDesc, newStatus);

            goal.Title.Should().Be(newTitle);
            goal.Description.Should().Be(newDesc);
            goal.Status.Should().Be(newStatus);
        }

        [Fact]
        public void Update_WithNullTitle_ShouldNotChangeTitle()
        {
            var goal = CreateValidGoal();
            var originalTitle = goal.Title;

            goal.Update(null, "New Desc", GoalStatus.InProgress);

            goal.Title.Should().Be(originalTitle);
        }

        [Fact]
        public void Update_WithEmptyTitle_ShouldNotChangeTitle()
        {
            var goal = CreateValidGoal();
            var originalTitle = goal.Title;

            goal.Update(string.Empty, "New Desc", GoalStatus.InProgress);

            goal.Title.Should().Be(originalTitle);
        }

        [Fact]
        public void Update_WithNullDescription_ShouldNotChangeDescription()
        {
            var goal = CreateValidGoal();
            var originalDesc = goal.Description;

            goal.Update("New Title", null, GoalStatus.InProgress);

            goal.Description.Should().Be(originalDesc);
        }

        [Fact]
        public void Update_WithEmptyDescription_ShouldNotChangeDescription()
        {
            var goal = CreateValidGoal();
            var originalDesc = goal.Description;

            goal.Update("New Title", string.Empty, GoalStatus.InProgress);

            goal.Description.Should().Be(originalDesc);
        }

        [Fact]
        public void Update_WithNullStatus_ShouldNotChangeStatus()
        {
            var goal = CreateValidGoal(status: GoalStatus.Pending);

            goal.Update("New Title", "New Desc", null);

            goal.Status.Should().Be(GoalStatus.Pending);
        }

        [Fact]
        public void Update_WithAllNulls_ShouldLeaveGoalUnchanged()
        {
            var goal = CreateValidGoal();
            var originalTitle = goal.Title;
            var originalDesc = goal.Description;
            var originalStatus = goal.Status;

            goal.Update(null, null, null);

            goal.Title.Should().Be(originalTitle);
            goal.Description.Should().Be(originalDesc);
            goal.Status.Should().Be(originalStatus);
        }
    }
}
