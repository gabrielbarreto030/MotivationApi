using System;
using System.Threading.Tasks;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;
using Motivation.Domain.Entities;
using System.Linq;
using Motivation.Domain.Interfaces;

namespace Motivation.Application.Services
{
    public class GoalService : IGoalService
    {
        private readonly IGoalRepository _goalRepository;

        public GoalService(IGoalRepository goalRepository)
        {
            _goalRepository = goalRepository;
        }

        public async Task<CreateGoalResponse> CreateAsync(CreateGoalRequest request, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required", nameof(request.Title));
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Description is required", nameof(request.Description));

            var goal = new Goal(Guid.NewGuid(), userId, request.Title, request.Description, GoalStatus.Pending, DateTime.UtcNow);
            await _goalRepository.AddAsync(goal);

            return new CreateGoalResponse(goal.Id, goal.Title, goal.Description, goal.Status, goal.CreatedAt);
        }

        public async Task<CreateGoalResponse[]> ListByUserAsync(Guid userId)
        {
            var goals = await _goalRepository.GetByUserAsync(userId);
            return goals.Select(g => new CreateGoalResponse(g.Id, g.Title, g.Description, g.Status, g.CreatedAt)).ToArray();
        }

        public async Task<UpdateGoalResponse> UpdateAsync(Guid id, UpdateGoalRequest request, Guid userId)
        {
            var goal = await _goalRepository.GetByIdAsync(id);
            if (goal == null)
                throw new ArgumentException("Goal not found", nameof(id));

            if (goal.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to update this goal");

            // Parse Status if provided
            GoalStatus? status = null;
            if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<GoalStatus>(request.Status, true, out var parsedStatus))
            {
                status = parsedStatus;
            }

            goal.Update(request.Title, request.Description, status);
            await _goalRepository.UpdateAsync(goal);

            return new UpdateGoalResponse(goal.Id, goal.Title, goal.Description, goal.Status, goal.CreatedAt);
        }
    }
}
