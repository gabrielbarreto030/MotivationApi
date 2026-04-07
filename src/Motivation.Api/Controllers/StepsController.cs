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
    /// Gerencia os passos (steps) de uma meta. Todos os endpoints requerem JWT.
    /// </summary>
    [ApiController]
    [Route("goals/{goalId}/steps")]
    [Authorize]
    [Produces("application/json")]
    public class StepsController : ControllerBase
    {
        private readonly IStepService _stepService;
        private readonly ILogger<StepsController> _logger;

        public StepsController(IStepService stepService, ILogger<StepsController> logger)
        {
            _stepService = stepService;
            _logger = logger;
        }

        /// <summary>
        /// Lista todos os passos de uma meta do usuário autenticado.
        /// </summary>
        /// <param name="goalId">Id da meta.</param>
        /// <returns>Array de passos associados à meta.</returns>
        /// <response code="200">Lista de passos retornada com sucesso.</response>
        /// <response code="400">GoalId inválido ou meta não encontrada.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        /// <summary>
        /// Cria um novo passo para uma meta do usuário autenticado.
        /// </summary>
        /// <param name="goalId">Id da meta à qual o passo será adicionado.</param>
        /// <param name="dto">Título do passo.</param>
        /// <returns>Passo criado com id e dados completos.</returns>
        /// <response code="201">Passo criado com sucesso.</response>
        /// <response code="400">Dados inválidos ou meta inexistente.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        /// <summary>
        /// Marca um passo como concluído, registrando a data/hora de conclusão.
        /// </summary>
        /// <param name="goalId">Id da meta.</param>
        /// <param name="stepId">Id do passo a concluir.</param>
        /// <returns>Passo atualizado com IsCompleted = true e CompletedAt preenchido.</returns>
        /// <response code="200">Passo marcado como concluído.</response>
        /// <response code="400">StepId inválido ou passo não pertence à meta.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        /// <response code="409">Passo já estava concluído anteriormente.</response>
        [HttpPut("{stepId}/complete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> MarkCompleted(Guid goalId, Guid stepId)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            try
            {
                var result = await _stepService.MarkCompletedAsync(goalId, stepId, userId);
                _logger.LogInformation("Step {StepId} marked as completed for goal {GoalId} by user {UserId}", stepId, goalId, userId);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request marking step {StepId} as completed: {Message}", stepId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Invalid operation marking step {StepId} as completed: {Message}", stepId, ex.Message);
                return Conflict(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Unauthorized step complete attempt on goal {GoalId} by user {UserId}", goalId, userId);
                return Forbid();
            }
        }
    }
}
