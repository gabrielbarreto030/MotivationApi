using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;

namespace Motivation.Api.Controllers
{
    /// <summary>
    /// Gerencia as frases motivacionais associadas a uma meta. Todos os endpoints requerem JWT.
    /// </summary>
    [ApiController]
    [Route("goals/{goalId}/motivations")]
    [Authorize]
    [Produces("application/json")]
    public class MotivationsController : ControllerBase
    {
        private readonly IMotivationService _motivationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<MotivationsController> _logger;

        public MotivationsController(IMotivationService motivationService, ICurrentUserService currentUserService, ILogger<MotivationsController> logger)
        {
            _motivationService = motivationService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Adiciona uma frase motivacional a uma meta do usuário autenticado.
        /// </summary>
        /// <param name="goalId">Id da meta.</param>
        /// <param name="dto">Texto da frase motivacional.</param>
        /// <returns>Motivação criada com id e texto.</returns>
        /// <response code="201">Motivação adicionada com sucesso.</response>
        /// <response code="400">Dados inválidos ou meta inexistente.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Add(Guid goalId, [FromBody] AddMotivationRequest dto)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var result = await _motivationService.AddAsync(goalId, dto, userId.Value);
                _logger.LogInformation("Motivation {MotivationId} added to goal {GoalId} by user {UserId}", result.Id, goalId, userId.Value);
                return CreatedAtAction(nameof(Add), new { goalId, id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request adding motivation to goal {GoalId}: {Message}", goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized motivation add attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId.Value, ex.Message);
                return Forbid();
            }
        }

        /// <summary>
        /// Remove uma frase motivacional de uma meta do usuário autenticado.
        /// </summary>
        /// <param name="goalId">Id da meta.</param>
        /// <param name="motivationId">Id da motivação a remover.</param>
        /// <response code="204">Motivação removida com sucesso.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        /// <response code="404">Motivação não encontrada na meta.</response>
        [HttpDelete("{motivationId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Remove(Guid goalId, Guid motivationId)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                await _motivationService.RemoveAsync(goalId, motivationId, userId.Value);
                _logger.LogInformation("Motivation {MotivationId} removed from goal {GoalId} by user {UserId}", motivationId, goalId, userId.Value);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request removing motivation {MotivationId} from goal {GoalId}: {Message}", motivationId, goalId, ex.Message);
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized motivation remove attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId.Value, ex.Message);
                return Forbid();
            }
        }
    }
}
