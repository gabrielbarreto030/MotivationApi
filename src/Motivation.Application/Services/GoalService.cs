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
    }
}