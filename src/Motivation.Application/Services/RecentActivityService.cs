using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;
using Motivation.Domain.Interfaces;

namespace Motivation.Application.Services
{
    public class RecentActivityService : IRecentActivityService
    {
        private readonly IGoalRepository _goalRepository;
        private readonly IStepRepository _stepRepository;
        private readonly ILogger<RecentActivityService> _logger;

        public RecentActivityService(
            IGoalRepository goalRepository,
            IStepRepository stepRepository,
            ILogger<RecentActivityService> logger)
        {
            _goalRepository = goalRepository;
            _stepRepository = stepRepository;
            _logger = logger;
        }

        public async Task<RecentActivityResponse> GetRecentActivityAsync(Guid userId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 100) pageSize = 100;

            var goals = await _goalRepository.GetByUserAsync(userId);

            var allEntries = new List<RecentActivityEntry>();

            foreach (var goal in goals)
            {
                var steps = await _stepRepository.GetByGoalAsync(goal.Id);
                foreach (var step in steps)
                {
                    if (!step.IsCompleted || !step.CompletedAt.HasValue)
                        continue;

                    allEntries.Add(new RecentActivityEntry(
                        step.Id,
                        step.Title,
                        goal.Id,
                        goal.Title,
                        step.CompletedAt.Value.ToUniversalTime()
                    ));
                }
            }

            var ordered = allEntries.OrderByDescending(e => e.CompletedAt).ToList();
            var totalCount = ordered.Count;

            var paged = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            _logger.LogInformation(
                "Recent activity feed for user {UserId}: {TotalCount} completed steps, page {Page}/{TotalPages}",
                userId, totalCount, page, (int)Math.Ceiling(totalCount / (double)pageSize));

            return new RecentActivityResponse(totalCount, page, pageSize, paged);
        }
    }
}
