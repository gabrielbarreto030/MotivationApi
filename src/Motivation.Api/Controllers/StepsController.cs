using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;

namespace Motivation.Api.Controllers
{
    [ApiController]
    [Route("goals/{goalId}/steps")]
    [Authorize]
    public class StepsController : ControllerBase
    {
        private readonly IStepService _stepService;
        private readonly ILogger<StepsController> _logger;

        public StepsController(IStepService stepService, ILogger<StepsController> logger)
        {
            _stepService = stepService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> List(Guid goalId)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            try
            {
                var steps = await _stepService.ListByGoalAsync(goalId, userId);
                _logger.LogInformation("Listed {Count} steps for goal {GoalId} by user {UserId}", steps.Length, goalId, userId);
                return Ok(steps);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request listing steps for goal {GoalId}: {Message}", goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Unauthorized step list attempt on goal {GoalId} by user {UserId}", goalId, userId);
                return Forbid();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(Guid goalId, [FromBody] CreateStepRequest dto)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            try
            {
                var result = await _stepService.CreateAsync(goalId, dto, userId);
                _logger.LogInformation("Step {StepId} created for goal {GoalId} by user {UserId}", result.Id, goalId, userId);
                return CreatedAtAction(nameof(Create), new { goalId, id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request creating step for goal {GoalId}: {Message}", goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized step creation attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId, ex.Message);
                return Forbid();
            }
        }
    }
}
