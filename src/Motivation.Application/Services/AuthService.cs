using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Motivation.Application.DTOs;
using Motivation.Application.Exceptions;
using Motivation.Application.Interfaces;
using Motivation.Domain.Entities;
using Motivation.Domain.Interfaces;

namespace Motivation.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository userRepository,
            ILogger<AuthService> logger,
            IJwtService jwtService,
            IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _logger = logger;
            _jwtService = jwtService;
            _passwordHasher = passwordHasher;
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email is required", nameof(request.Email));
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Password is required", nameof(request.Password));

            var existing = await _userRepository.GetByEmailAsync(request.Email);
            if (existing != null)
                throw new EmailAlreadyInUseException(request.Email);

            var hashed = _passwordHasher.Hash(request.Password);
            var user = new User(Guid.NewGuid(), request.Email, hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);

            _logger.LogInformation("User {UserId} registered with email '{Email}'", user.Id, user.Email);

            return new RegisterResponse(user.Id, user.Email);
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email is required", nameof(request.Email));
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Password is required", nameof(request.Password));

            var existing = await _userRepository.GetByEmailAsync(request.Email);
            if (existing == null)
            {
                _logger.LogWarning("Login attempt failed for email '{Email}': user not found", request.Email);
                throw new AuthenticationFailedException("Invalid credentials");
            }

            if (!_passwordHasher.Verify(request.Password, existing.PasswordHash))
            {
                _logger.LogWarning("Login attempt failed for user {UserId}: invalid password", existing.Id);
                throw new AuthenticationFailedException("Invalid credentials");
            }

            var token = _jwtService.GenerateToken(existing);

            _logger.LogInformation("User {UserId} logged in successfully", existing.Id);

            return new LoginResponse(existing.Id, existing.Email, token);
        }
    }
}
