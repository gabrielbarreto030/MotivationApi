using System;
using Motivation.Application.Interfaces;

namespace Motivation.Application.Services
{
    public class GoalProgressCalculator : IGoalProgressCalculator
    {
        public double Calculate(int total, int completed)
        {
            if (total == 0) return 0;
            return Math.Round((double)completed / total * 100, 2);
        }
    }
}
