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
    /// Gerencia metas (goals) do usuário autenticado.
    /// Todos os endpoints requerem autenticação JWT.
    /// </summary>
    [ApiController]
    [Route("goals")]
    [Authorize]
    [Produces("application/json")]
    public class GoalsController : ControllerBase
    {
        private readonly IGoalService _goalService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<GoalsController> _logger;

        public GoalsController(IGoalService goalService, ICurrentUserService currentUserService, ILogger<GoalsController> logger)
        {
            _goalService = goalService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Retorna um resumo estatístico das metas e passos do usuário autenticado.
        /// </summary>
        /// <returns>Totais de metas por status, passos totais/concluídos e taxa de conclusão geral.</returns>
        /// <response code="200">Resumo retornado com sucesso.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        [HttpGet("summary")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSummary()
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _goalService.GetSummaryAsync(userId.Value);
            return Ok(result);
        }

        /// <summary>
        /// Retorna as metas vencidas (com deadline no passado e não concluídas/canceladas) do usuário autenticado.
        /// </summary>
        /// <returns>Lista de metas cujo prazo já passou e ainda estão ativas.</returns>
        /// <response code="200">Lista de metas vencidas retornada com sucesso.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        [HttpGet("overdue")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetOverdue()
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _goalService.GetOverdueAsync(userId.Value);
            return Ok(result);
        }

        /// <summary>
        /// Cria uma nova meta para o usuário autenticado.
        /// </summary>
        /// <param name="dto">Título, descrição e status da meta.</param>
        /// <returns>Meta criada com id e dados completos.</returns>
        /// <response code="201">Meta criada com sucesso.</response>
        /// <response code="400">Dados inválidos.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] CreateGoalRequest dto)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _goalService.CreateAsync(dto, userId.Value);
            return CreatedAtAction(nameof(Create), new { id = result.Id }, result);
        }

        /// <summary>
        /// Lista as metas do usuário autenticado com paginação, filtro e ordenação opcionais.
        /// </summary>
        /// <param name="page">Número da página (padrão: 1).</param>
        /// <param name="pageSize">Itens por página (padrão: 10, máximo: 50).</param>
        /// <param name="status">Filtrar por status: Pending, InProgress, Completed, Cancelled (opcional).</param>
        /// <param name="sortBy">Campo de ordenação: title, status, createdAt (padrão: createdAt).</param>
        /// <param name="sortOrder">Direção: asc (padrão) ou desc.</param>
        /// <returns>Resposta paginada com metas do usuário.</returns>
        /// <response code="200">Lista paginada retornada com sucesso.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            Motivation.Domain.Entities.GoalStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<Motivation.Domain.Entities.GoalStatus>(status, ignoreCase: true, out var parsed))
                statusFilter = parsed;

            var filterRequest = new GoalFilterRequest(page, pageSize, statusFilter, sortBy, sortOrder);
            var result = await _goalService.ListByUserFilteredAsync(userId.Value, filterRequest);
            return Ok(result);
        }

        /// <summary>
        /// Atualiza título, descrição ou status de uma meta existente.
        /// </summary>
        /// <param name="id">Id da meta a atualizar.</param>
        /// <param name="dto">Campos a atualizar (todos opcionais).</param>
        /// <returns>Meta atualizada.</returns>
        /// <response code="200">Meta atualizada com sucesso.</response>
        /// <response code="400">Dados inválidos.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="404">Meta não encontrada ou não pertence ao usuário.</response>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGoalRequest dto)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _goalService.UpdateAsync(id, dto, userId.Value);
            _logger.LogInformation("Goal {GoalId} updated by user {UserId}", id, userId.Value);
            return Ok(result);
        }

        /// <summary>
        /// Calcula o progresso de uma meta com base nos passos concluídos.
        /// </summary>
        /// <param name="id">Id da meta.</param>
        /// <returns>Percentual de progresso e totais de passos.</returns>
        /// <response code="200">Progresso calculado com sucesso.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="404">Meta não encontrada ou não pertence ao usuário.</response>
        [HttpGet("{id}/progress")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProgress(Guid id)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _goalService.GetProgressAsync(id, userId.Value);
            return Ok(result);
        }

        /// <summary>
        /// Remove permanentemente uma meta do usuário autenticado.
        /// </summary>
        /// <param name="id">Id da meta a remover.</param>
        /// <response code="204">Meta removida com sucesso.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="404">Meta não encontrada ou não pertence ao usuário.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            await _goalService.DeleteAsync(id, userId.Value);
            _logger.LogInformation("Goal {GoalId} deleted by user {UserId}", id, userId.Value);
            return NoContent();
        }
    }
}
