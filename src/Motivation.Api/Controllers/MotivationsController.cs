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
    [Route("goals/{goalId}/motivations")]
    [Authorize]
    public class MotivationsController : ControllerBase
    {
        private readonly IMotivationService _motivationService;
        private readonly ILogger<MotivationsController> _logger;

        public MotivationsController(IMotivationService motivationService, ILogger<MotivationsController> logger)
        {
            _motivationService = motivationService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Add(Guid goalId, [FromBody] AddMotivationRequest dto)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            try
            {
                var result = await _motivationService.AddAsync(goalId, dto, userId);
                _logger.LogInformation("Motivation {MotivationId} added to goal {GoalId} by user {UserId}", result.Id, goalId, userId);
                return CreatedAtAction(nameof(Add), new { goalId, id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request adding motivation to goal {GoalId}: {Message}", goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized motivation add attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId, ex.Message);
                return Forbid();
            }
        }

        [HttpDelete("{motivationId}")]
        public async Task<IActionResult> Remove(Guid goalId, Guid motivationId)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            try
            {
                await _motivationService.RemoveAsync(goalId, motivationId, userId);
                _logger.LogInformation("Motivation {MotivationId} removed from goal {GoalId} by user {UserId}", motivationId, goalId, userId);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request removing motivation {MotivationId} from goal {GoalId}: {Message}", motivationId, goalId, ex.Message);
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized motivation remove attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId, ex.Message);
                return Forbid();
            }
        }
    }
}
