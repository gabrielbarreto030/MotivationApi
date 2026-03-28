using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;
using Motivation.Domain.Interfaces;

namespace Motivation.Application.Services
{
    public class MotivationService : IMotivationService
    {
        private readonly IMotivationRepository _motivationRepository;
        private readonly IGoalRepository _goalRepository;

        public MotivationService(IMotivationRepository motivationRepository, IGoalRepository goalRepository)
        {
            _motivationRepository = motivationRepository;
            _goalRepository = goalRepository;
        }

        public async Task<AddMotivationResponse> AddAsync(Guid goalId, AddMotivationRequest request, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                throw new ArgumentException("Text is required", nameof(request.Text));

            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to add motivations to this goal");

            var motivation = new Domain.Entities.Motivation(Guid.NewGuid(), goalId, request.Text);
            await _motivationRepository.AddAsync(motivation);

            return new AddMotivationResponse(motivation.Id, motivation.GoalId, motivation.Text);
        }
    }
}
