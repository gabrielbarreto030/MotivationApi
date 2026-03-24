using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;
using Motivation.Domain.Entities;
using Motivation.Domain.Interfaces;

namespace Motivation.Application.Services
{
    public class StepService : IStepService
    {
        private readonly IStepRepository _stepRepository;
        private readonly IGoalRepository _goalRepository;

        public StepService(IStepRepository stepRepository, IGoalRepository goalRepository)
        {
            _stepRepository = stepRepository;
            _goalRepository = goalRepository;
        }

        public async Task<CreateStepResponse> CreateAsync(Guid goalId, CreateStepRequest request, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required", nameof(request.Title));

            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to add steps to this goal");

            var step = new Step(Guid.NewGuid(), goalId, request.Title);
            await _stepRepository.AddAsync(step);

            return new CreateStepResponse(step.Id, step.GoalId, step.Title, step.IsCompleted, step.CompletedAt);
        }
    }
}
