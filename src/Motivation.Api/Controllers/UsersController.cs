using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Motivation.Api.Models;
using Motivation.Application.DTOs;
using Motivation.Application.Exceptions;
using Motivation.Application.Interfaces;
using Motivation.Application.Services;

namespace Motivation.Api.Controllers
{
    [ApiController]
    [Route("users")]
    public class UsersController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IJwtService _jwtService;
        private readonly IUserStatsService _userStatsService;
        private readonly IStreakService _streakService;
        private readonly IWeeklyReportService _weeklyReportService;
        private readonly IMonthlyReportService _monthlyReportService;
        private readonly IYearlyReportService _yearlyReportService;
        private readonly IActivityHeatmapService _activityHeatmapService;
        private readonly IRecentActivityService _recentActivityService;
        private readonly IDailySummaryService _dailySummaryService;
        private readonly IWeekdayStatsService _weekdayStatsService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IAuthService authService, IJwtService jwtService, IUserStatsService userStatsService, IStreakService streakService, IWeeklyReportService weeklyReportService, IMonthlyReportService monthlyReportService, IYearlyReportService yearlyReportService, IActivityHeatmapService activityHeatmapService, IRecentActivityService recentActivityService, IDailySummaryService dailySummaryService, IWeekdayStatsService weekdayStatsService, ILogger<UsersController> logger)
        {
            _authService = authService;
            _jwtService = jwtService;
            _userStatsService = userStatsService;
            _streakService = streakService;
            _weeklyReportService = weeklyReportService;
            _monthlyReportService = monthlyReportService;
            _yearlyReportService = yearlyReportService;
            _activityHeatmapService = activityHeatmapService;
            _recentActivityService = recentActivityService;
            _dailySummaryService = dailySummaryService;
            _weekdayStatsService = weekdayStatsService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            try
            {
                var result = await _authService.RegisterAsync(new RegisterRequest(dto.Email, dto.Password));
                return CreatedAtAction(nameof(Register), new { id = result.UserId }, new { result.UserId, result.Email });
            }
            catch (EmailAlreadyInUseException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            try
            {
                var user = await _authService.ValidateCredentialsAsync(new LoginRequest(dto.Email, dto.Password));
                var token = _jwtService.GenerateToken(user);
                _logger.LogInformation("User {UserId} logged in successfully", user.Id);
                return Ok(new { UserId = user.Id, Email = user.Email, Token = token });
            }
            catch (AuthenticationFailedException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null)
                return Unauthorized();

            return Ok(new { userId = userIdClaim.Value, message = "Access granted with valid token" });
        }

        [Authorize]
        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null)
                return Unauthorized();

            if (!Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            try
            {
                await _authService.ChangePasswordAsync(userId, new ChangePasswordRequest(dto.CurrentPassword, dto.NewPassword));
                return NoContent();
            }
            catch (AuthenticationFailedException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("email")]
        public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequestDto dto)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null)
                return Unauthorized();

            if (!Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            try
            {
                await _authService.ChangeEmailAsync(userId, new ChangeEmailRequest(dto.CurrentPassword, dto.NewEmail));
                _logger.LogInformation("User {UserId} changed their email", userId);
                return NoContent();
            }
            catch (AuthenticationFailedException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (EmailAlreadyInUseException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var stats = await _userStatsService.GetStatsAsync(userId);
            _logger.LogInformation("Stats retrieved for user {UserId}", userId);
            return Ok(stats);
        }

        [Authorize]
        [HttpGet("streak")]
        public async Task<IActionResult> GetStreak()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _streakService.GetStreakAsync(userId);
            _logger.LogInformation("Streak retrieved for user {UserId}: current={Current}, longest={Longest}",
                userId, result.CurrentStreak, result.LongestStreak);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("weekly-report")]
        public async Task<IActionResult> GetWeeklyReport()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _weeklyReportService.GetWeeklyReportAsync(userId);
            _logger.LogInformation(
                "Weekly report retrieved for user {UserId}: {TotalSteps} steps, {GoalsProgressed} goals progressed",
                userId, result.TotalStepsCompleted, result.TotalGoalsProgressed);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("monthly-report")]
        public async Task<IActionResult> GetMonthlyReport()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _monthlyReportService.GetMonthlyReportAsync(userId);
            _logger.LogInformation(
                "Monthly report retrieved for user {UserId}: {TotalSteps} steps, {GoalsProgressed} goals progressed",
                userId, result.TotalStepsCompleted, result.TotalGoalsProgressed);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("yearly-report")]
        public async Task<IActionResult> GetYearlyReport()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _yearlyReportService.GetYearlyReportAsync(userId);
            _logger.LogInformation(
                "Yearly report retrieved for user {UserId}: {TotalSteps} steps, {GoalsProgressed} goals progressed",
                userId, result.TotalStepsCompleted, result.TotalGoalsProgressed);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("activity-heatmap")]
        public async Task<IActionResult> GetActivityHeatmap()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _activityHeatmapService.GetHeatmapAsync(userId);
            _logger.LogInformation(
                "Activity heatmap retrieved for user {UserId}: {TotalSteps} steps, {ActiveDays} active days",
                userId, result.TotalStepsCompleted, result.ActiveDays);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("recent-activity")]
        public async Task<IActionResult> GetRecentActivity([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _recentActivityService.GetRecentActivityAsync(userId, page, pageSize);
            _logger.LogInformation(
                "Recent activity feed for user {UserId}: {TotalCount} total, page {Page}",
                userId, result.TotalCount, result.Page);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("daily-summary")]
        public async Task<IActionResult> GetDailySummary([FromQuery] string? date = null)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            DateOnly queryDate;
            if (date == null)
            {
                queryDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }
            else if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out queryDate))
            {
                return BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });
            }

            var result = await _dailySummaryService.GetDailySummaryAsync(userId, queryDate);
            _logger.LogInformation(
                "Daily summary for user {UserId} on {Date}: {TotalSteps} steps, {GoalsProgressed} goals",
                userId, queryDate, result.TotalStepsCompleted, result.GoalsProgressed);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("weekday-stats")]
        public async Task<IActionResult> GetWeekdayStats()
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var result = await _weekdayStatsService.GetWeekdayStatsAsync(userId);
            _logger.LogInformation(
                "Weekday stats for user {UserId}: {TotalSteps} steps, most productive: {MostProductiveDay}",
                userId, result.TotalStepsCompleted, result.MostProductiveDay ?? "none");
            return Ok(result);
        }

        [Authorize]
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequestDto dto)
        {
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null)
                return Unauthorized();

            if (!Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            try
            {
                await _authService.DeleteAccountAsync(userId, dto.Password);
                _logger.LogInformation("User {UserId} deleted their account", userId);
                return NoContent();
            }
            catch (AuthenticationFailedException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
