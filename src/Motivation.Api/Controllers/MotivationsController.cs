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
        /// Lista as frases motivacionais de uma meta do usuário autenticado, com busca, filtragem por tag/favoritos e ordenação opcionais.
        /// </summary>
        /// <param name="goalId">Id da meta.</param>
        /// <param name="search">Termo de busca parcial e case-insensitive no texto da motivação (opcional).</param>
        /// <param name="tag">Filtra motivações que possuam exatamente esta tag (case-insensitive, opcional).</param>
        /// <param name="sortBy">Campo de ordenação: "text" ou "createdAt" (padrão: createdAt).</param>
        /// <param name="sortOrder">Direção de ordenação: "asc" ou "desc" (padrão: asc).</param>
        /// <param name="onlyFavorites">Se true, retorna apenas motivações marcadas como favoritas (opcional).</param>
        /// <param name="page">Número da página (padrão: 1).</param>
        /// <param name="pageSize">Itens por página (padrão: 10).</param>
        /// <returns>Lista paginada de motivações correspondentes.</returns>
        /// <response code="200">Lista retornada com sucesso.</response>
        /// <response code="400">Meta não encontrada.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(Guid goalId, [FromQuery] string? search = null, [FromQuery] string? tag = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortOrder = null, [FromQuery] bool? onlyFavorites = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var filter = new MotivationFilterRequest(page, pageSize, search, sortBy, sortOrder, tag, onlyFavorites);
                var result = await _motivationService.ListByGoalFilteredAsync(goalId, userId.Value, filter);
                _logger.LogInformation("Listed {Count} motivations for goal {GoalId} by user {UserId} (search={Search}, tag={Tag}, sortBy={SortBy}, sortOrder={SortOrder}, onlyFavorites={OnlyFavorites})", result.TotalCount, goalId, userId.Value, search, tag, sortBy, sortOrder, onlyFavorites);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request listing motivations for goal {GoalId}: {Message}", goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized motivation list attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId.Value, ex.Message);
                return Forbid();
            }
        }

        /// <summary>
        /// Marca uma frase motivacional como favorita.
        /// </summary>
        /// <param name="goalId">Id da meta.</param>
        /// <param name="motivationId">Id da motivação a favoritar.</param>
        /// <returns>Motivação atualizada com IsFavorite=true.</returns>
        /// <response code="200">Motivação favoritada com sucesso.</response>
        /// <response code="400">Motivação não encontrada ou não pertence à meta.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        /// <response code="409">Motivação já é favorita.</response>
        [HttpPost("{motivationId}/favorite")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Favorite(Guid goalId, Guid motivationId)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var result = await _motivationService.FavoriteAsync(goalId, motivationId, userId.Value);
                _logger.LogInformation("Motivation {MotivationId} marked as favorite on goal {GoalId} by user {UserId}", motivationId, goalId, userId.Value);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Conflict favoriting motivation {MotivationId} on goal {GoalId}: {Message}", motivationId, goalId, ex.Message);
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request favoriting motivation {MotivationId} on goal {GoalId}: {Message}", motivationId, goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized favorite attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId.Value, ex.Message);
                return Forbid();
            }
        }

        /// <summary>
        /// Remove uma frase motivacional dos favoritos.
        /// </summary>
        /// <param name="goalId">Id da meta.</param>
        /// <param name="motivationId">Id da motivação a desfavoritar.</param>
        /// <returns>Motivação atualizada com IsFavorite=false.</returns>
        /// <response code="200">Motivação removida dos favoritos com sucesso.</response>
        /// <response code="400">Motivação não encontrada ou não pertence à meta.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        /// <response code="409">Motivação não é favorita.</response>
        [HttpDelete("{motivationId}/favorite")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Unfavorite(Guid goalId, Guid motivationId)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var result = await _motivationService.UnfavoriteAsync(goalId, motivationId, userId.Value);
                _logger.LogInformation("Motivation {MotivationId} removed from favorites on goal {GoalId} by user {UserId}", motivationId, goalId, userId.Value);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Conflict unfavoriting motivation {MotivationId} on goal {GoalId}: {Message}", motivationId, goalId, ex.Message);
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request unfavoriting motivation {MotivationId} on goal {GoalId}: {Message}", motivationId, goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized unfavorite attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId.Value, ex.Message);
                return Forbid();
            }
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
        /// Atualiza o texto de uma frase motivacional de uma meta do usuário autenticado.
        /// </summary>
        /// <param name="goalId">Id da meta.</param>
        /// <param name="motivationId">Id da motivação a atualizar.</param>
        /// <param name="dto">Novo texto da frase motivacional.</param>
        /// <returns>Motivação atualizada com id e texto.</returns>
        /// <response code="200">Motivação atualizada com sucesso.</response>
        /// <response code="400">Dados inválidos, meta ou motivação inexistente.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="403">Meta pertence a outro usuário.</response>
        [HttpPut("{motivationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Update(Guid goalId, Guid motivationId, [FromBody] UpdateMotivationRequest dto)
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var result = await _motivationService.UpdateAsync(goalId, motivationId, dto, userId.Value);
                _logger.LogInformation("Motivation {MotivationId} updated on goal {GoalId} by user {UserId}", motivationId, goalId, userId.Value);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request updating motivation {MotivationId} on goal {GoalId}: {Message}", motivationId, goalId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized motivation update attempt on goal {GoalId} by user {UserId}: {Message}", goalId, userId.Value, ex.Message);
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
