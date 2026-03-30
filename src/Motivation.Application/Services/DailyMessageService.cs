using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;
using Motivation.Domain.Interfaces;

namespace Motivation.Application.Services
{
    public class DailyMessageService : IDailyMessageService
    {
        private const string DefaultMessage = "Keep going! Every step forward is progress.";

        private readonly IGoalRepository _goalRepository;
        private readonly IMotivationRepository _motivationRepository;

        public DailyMessageService(IGoalRepository goalRepository, IMotivationRepository motivationRepository)
        {
            _goalRepository = goalRepository;
            _motivationRepository = motivationRepository;
        }

        public async Task<DailyMessageResponse> GetDailyMessageAsync(Guid userId, DateOnly? date = null)
        {
            var today = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var goals = await _goalRepository.GetByUserAsync(userId);

            var allTexts = new List<string>();
            foreach (var goal in goals)
            {
                var motivations = await _motivationRepository.GetByGoalAsync(goal.Id);
                allTexts.AddRange(motivations.Select(m => m.Text));
            }

            var message = allTexts.Count == 0
                ? DefaultMessage
                : allTexts[today.DayOfYear % allTexts.Count];

            return new DailyMessageResponse(message, today);
        }
    }
}
