namespace Motivation.Application.Interfaces
{
    public interface IGoalProgressCalculator
    {
        double Calculate(int total, int completed);
    }
}
