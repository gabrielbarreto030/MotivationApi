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
        private readonly ILogger<GoalsController> _logger;

        public GoalsController(IGoalService goalService, ILogger<GoalsController> logger)
        {
            _goalService = goalService;
            _logger = logger;
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
            _logger.LogInformation("Authorization header received: {Header}", Request.Headers["Authorization"].ToString());

            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _goalService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(Create), new { id = result.Id }, result);
        }

        /// <summary>
        /// Lista todas as metas do usuário autenticado.
        /// Resultados podem ser servidos via cache.
        /// </summary>
        /// <returns>Lista de metas do usuário.</returns>
        /// <response code="200">Lista retornada com sucesso (pode ser vazia).</response>
        /// <response code="401">Token ausente ou inválido.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> List()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var list = await _goalService.ListByUserAsync(userId);
            return Ok(list);
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
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _goalService.UpdateAsync(id, dto, userId);
            _logger.LogInformation("Goal {GoalId} updated by user {UserId}", id, userId);
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
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _goalService.GetProgressAsync(id, userId);
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
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            await _goalService.DeleteAsync(id, userId);
            _logger.LogInformation("Goal {GoalId} deleted by user {UserId}", id, userId);
            return NoContent();
        }
    }
}
