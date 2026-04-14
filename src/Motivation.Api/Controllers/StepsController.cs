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
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<StepsController> _logger;

        public StepsController(IStepService stepService, ICurrentUserService currentUserService, ILogger<StepsController> logger)
        {
            _stepService = stepService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Lista os passos de uma meta com paginação, filtro e ordenação opcionais.
        /// </summary>
        /// <param name="goalId">Id da meta.</param>
        /// <param name="page">Número da página (padrão: 1).</param>
        /// <param name="pageSize">Itens por página (padrão: 10, máximo: 50).</param>
        /// <param name="isCompleted">Filtrar por conclusão: true = concluídos, false = pendentes (opcional).</param>
        /// <param name="sortBy">Campo de ordenação: title (padrão), isCompleted, completedAt.</param>
        /// <param name="sortOrder">Direção: asc (padrão) ou desc.</param>
        /// <returns>Resposta paginada com passos da meta.</returns>
        /// <response code="200">Lista paginada de passos retornada com sucesso.</response>
        /// <response code="400">GoalId inválido ou meta não encontrada.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(
            Guid goalId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isCompleted = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var filterRequest = new StepFilterRequest(page, pageSize, isCompleted, sortBy, sortOrder);
                var result = await _stepService.ListByGoalFilteredAsync(goalId, userId.Value, filterRequest);
                _logger.LogInformation(
                    "Listed {Count}/{Total} steps for goal {GoalId} by user {UserId} (isCompleted: {IsCompleted}, sortBy: {SortBy}, sortOrder: {SortOrder})",
                    result.Items.Count, result.TotalCount, goalId, userId.Value,
                    isCompleted?.ToString() ?? "all", sortBy ?? "title", sortOrder ?? "asc");
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request listing steps for goal {GoalId}: {Message}", goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Unauthorized step list attempt on goal {GoalId} by user {UserId}", goalId, userId.Value);
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
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var result = await _stepService.CreateAsync(goalId, dto, userId.Value);
                _logger.LogInformation("Step {StepId} created for goal {GoalId} by user {UserId}", result.Id, goalId, userId.Value);
                return CreatedAtAction(nameof(Create), new { goalId, id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request creating step for goal {GoalId}: {Message}", goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized step creation attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId.Value, ex.Message);
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
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var result = await _stepService.MarkCompletedAsync(goalId, stepId, userId.Value);
                _logger.LogInformation("Step {StepId} marked as completed for goal {GoalId} by user {UserId}", stepId, goalId, userId.Value);
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
                _logger.LogWarning("Unauthorized step complete attempt on goal {GoalId} by user {UserId}", goalId, userId.Value);
                return Forbid();
            }
        }
    }
}
