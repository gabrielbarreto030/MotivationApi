using System;
using System.Threading.Tasks;
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
        private readonly Motivation.Application.Interfaces.IJwtService _jwtService;

        public AuthService(IUserRepository userRepository, Motivation.Application.Interfaces.IJwtService jwtService = null)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            // simple validation
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email is required", nameof(request.Email));
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Password is required", nameof(request.Password));

            var existing = await _userRepository.GetByEmailAsync(request.Email);
            if (existing != null)
                throw new EmailAlreadyInUseException(request.Email);

            var hashed = PasswordHasher.Hash(request.Password);
            var user = new User(Guid.NewGuid(), request.Email, hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);

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
                throw new Motivation.Application.Exceptions.AuthenticationFailedException("Invalid credentials");

            if (!PasswordHasher.Verify(request.Password, existing.PasswordHash))
                throw new Motivation.Application.Exceptions.AuthenticationFailedException("Invalid credentials");

            var token = _jwtService?.GenerateToken(existing) ?? string.Empty;

            return new LoginResponse(existing.Id, existing.Email, token);
        }
    }
}