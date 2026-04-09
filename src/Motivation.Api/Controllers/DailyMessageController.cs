using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Motivation.Application.Interfaces;

namespace Motivation.Api.Controllers
{
    /// <summary>
    /// Fornece a mensagem motivacional diária personalizada para o usuário autenticado.
    /// </summary>
    [ApiController]
    [Route("daily-message")]
    [Authorize]
    [Produces("application/json")]
    public class DailyMessageController : ControllerBase
    {
        private readonly IDailyMessageService _dailyMessageService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DailyMessageController> _logger;

        public DailyMessageController(IDailyMessageService dailyMessageService, ICurrentUserService currentUserService, ILogger<DailyMessageController> logger)
        {
            _dailyMessageService = dailyMessageService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Retorna a mensagem motivacional diária do usuário autenticado.
        /// A mensagem é gerada a partir das frases cadastradas nas metas do usuário.
        /// Se não houver frases, retorna uma mensagem padrão.
        /// </summary>
        /// <returns>Objeto com a mensagem motivacional do dia.</returns>
        /// <response code="200">Mensagem diária retornada com sucesso.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Get()
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _dailyMessageService.GetDailyMessageAsync(userId.Value);

            _logger.LogInformation("Daily message retrieved for user {UserId}: {Message}", userId.Value, result.Message);

            return Ok(result);
        }
    }
}
