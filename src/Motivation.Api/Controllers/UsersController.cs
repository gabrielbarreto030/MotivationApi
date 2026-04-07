using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Motivation.Api.Models;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;

namespace Motivation.Api.Controllers
{
    /// <summary>
    /// Gerencia registro, autenticação e perfil de usuários.
    /// </summary>
    [ApiController]
    [Route("users")]
    [Produces("application/json")]
    public class UsersController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IAuthService authService, ILogger<UsersController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Registra um novo usuário na plataforma.
        /// </summary>
        /// <param name="dto">Email e senha do novo usuário.</param>
        /// <returns>Dados do usuário criado (id e email).</returns>
        /// <response code="201">Usuário criado com sucesso.</response>
        /// <response code="400">Dados inválidos (email ou senha ausentes).</response>
        /// <response code="409">Email já cadastrado.</response>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            var result = await _authService.RegisterAsync(new RegisterRequest(dto.Email, dto.Password));
            return CreatedAtAction(nameof(Register), new { id = result.UserId }, new { result.UserId, result.Email });
        }

        /// <summary>
        /// Autentica um usuário e retorna um token JWT.
        /// </summary>
        /// <param name="dto">Email e senha do usuário.</param>
        /// <returns>Token JWT para uso nos endpoints protegidos.</returns>
        /// <response code="200">Login efetuado. Retorna userId, email e token JWT.</response>
        /// <response code="400">Dados inválidos.</response>
        /// <response code="401">Credenciais incorretas.</response>
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            var result = await _authService.LoginAsync(new LoginRequest(dto.Email, dto.Password));
            return Ok(new { result.UserId, result.Email, Token = result.Token });
        }

        /// <summary>
        /// Retorna o perfil do usuário autenticado. Requer JWT.
        /// </summary>
        /// <returns>UserId do usuário e mensagem de confirmação.</returns>
        /// <response code="200">Perfil retornado com sucesso.</response>
        /// <response code="401">Token ausente ou inválido.</response>
        [Authorize]
        [HttpGet("profile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetProfile()
        {
            _logger.LogInformation("UsersController profile called; Authorization header: {Header}", Request.Headers["Authorization"].ToString());

            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null)
                return Unauthorized();

            return Ok(new { userId = userIdClaim.Value, message = "Access granted with valid token" });
        }
    }
}
