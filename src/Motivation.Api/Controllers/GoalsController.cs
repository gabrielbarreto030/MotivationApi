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
    [Route("goals")]
    [Authorize]
    public class GoalsController : ControllerBase
    {
        private readonly IGoalService _goalService;
        private readonly ILogger<GoalsController> _logger;

        public GoalsController(IGoalService goalService, ILogger<GoalsController> logger)
        {
            _goalService = goalService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGoalRequest dto)
        {
            _logger.LogInformation("Authorization header received: {Header}", Request.Headers["Authorization"].ToString());

            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _goalService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(Create), new { id = result.Id }, result);
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var list = await _goalService.ListByUserAsync(userId);
            return Ok(list);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGoalRequest dto)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _goalService.UpdateAsync(id, dto, userId);
            _logger.LogInformation("Goal {GoalId} updated by user {UserId}", id, userId);
            return Ok(result);
        }

        [HttpGet("{id}/progress")]
        public async Task<IActionResult> GetProgress(Guid id)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _goalService.GetProgressAsync(id, userId);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            await _goalService.DeleteAsync(id, userId);
            _logger.LogInformation("Goal {GoalId} deleted by user {UserId}", id, userId);
            return NoContent();
        }
    }
}