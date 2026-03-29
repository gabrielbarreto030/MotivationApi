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

        public async Task RemoveAsync(Guid goalId, Guid motivationId, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(goalId);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(goalId));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to remove motivations from this goal");

            var motivation = await _motivationRepository.GetByIdAsync(motivationId);
            if (motivation == null)
                throw new ArgumentException("Motivation not found", nameof(motivationId));

            if (motivation.GoalId != goalId)
                throw new ArgumentException("Motivation does not belong to this goal", nameof(motivationId));

            await _motivationRepository.DeleteAsync(motivationId);
        }
    }
}
