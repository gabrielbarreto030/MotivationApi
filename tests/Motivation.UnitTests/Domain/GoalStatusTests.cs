using FluentAssertions;
using Motivation.Domain.Entities;
using Xunit;

namespace Motivation.UnitTests.DomainLayer
{
    public class GoalStatusTests
    {
        [Fact]
        public void GoalStatus_ShouldHavePendingValue()
        {
            ((int)GoalStatus.Pending).Should().Be(0);
        }

        [Fact]
        public void GoalStatus_ShouldHaveInProgressValue()
        {
            GoalStatus.InProgress.Should().BeDefined();
        }

        [Fact]
        public void GoalStatus_ShouldHaveCompletedValue()
        {
            GoalStatus.Completed.Should().BeDefined();
        }

        [Fact]
        public void GoalStatus_ShouldHaveCancelledValue()
        {
            GoalStatus.Cancelled.Should().BeDefined();
        }

        [Fact]
        public void GoalStatus_AllExpectedValues_ShouldExist()
        {
            var values = System.Enum.GetValues<GoalStatus>();

            values.Should().Contain(GoalStatus.Pending);
            values.Should().Contain(GoalStatus.InProgress);
            values.Should().Contain(GoalStatus.Completed);
            values.Should().Contain(GoalStatus.Cancelled);
        }

        [Fact]
        public void GoalStatus_ShouldHaveFourDistinctValues()
        {
            var values = System.Enum.GetValues<GoalStatus>();

            values.Should().HaveCount(4);
        }

        [Theory]
        [InlineData(GoalStatus.Pending, "Pending")]
        [InlineData(GoalStatus.InProgress, "InProgress")]
        [InlineData(GoalStatus.Completed, "Completed")]
        [InlineData(GoalStatus.Cancelled, "Cancelled")]
        public void GoalStatus_ToStringRepresentation_ShouldMatch(GoalStatus status, string expected)
        {
            status.ToString().Should().Be(expected);
        }
    }
}
