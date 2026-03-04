using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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

        public UsersController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            try
            {
                var result = await _authService.RegisterAsync(new RegisterRequest(dto.Email, dto.Password));
                // return created with location header pointing to a hypothetical GET user endpoint
                return CreatedAtAction(nameof(Register), new { id = result.UserId }, new { result.UserId, result.Email });
            }
            catch (EmailAlreadyInUseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            try
            {
                var result = await _authService.LoginAsync(new LoginRequest(dto.Email, dto.Password));
                return Ok(new { result.UserId, result.Email, Token = result.Token });
            }
            catch (AuthenticationFailedException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}