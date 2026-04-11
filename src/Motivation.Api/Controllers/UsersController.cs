using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Motivation.Api.Models;
using Motivation.Application.DTOs;
using Motivation.Application.Exceptions;
using Motivation.Application.Interfaces;

namespace Motivation.Api.Controllers
{
    [ApiController]
    [Route("users")]
    public class UsersController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IJwtService _jwtService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IAuthService authService, IJwtService jwtService, ILogger<UsersController> logger)
        {
            _authService = authService;
            _jwtService = jwtService;
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
    }
}
