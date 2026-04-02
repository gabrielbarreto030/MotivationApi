using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Motivation.Application.Interfaces;

namespace Motivation.Api.Controllers
{
    [ApiController]
    [Route("daily-message")]
    [Authorize]
    public class DailyMessageController : ControllerBase
    {
        private readonly IDailyMessageService _dailyMessageService;
        private readonly ILogger<DailyMessageController> _logger;

        public DailyMessageController(IDailyMessageService dailyMessageService, ILogger<DailyMessageController> logger)
        {
            _dailyMessageService = dailyMessageService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _dailyMessageService.GetDailyMessageAsync(userId);

            _logger.LogInformation("Daily message retrieved for user {UserId}: {Message}", userId, result.Message);

            return Ok(result);
        }
    }
}
