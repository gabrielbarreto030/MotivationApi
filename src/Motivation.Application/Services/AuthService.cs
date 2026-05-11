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
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepository, ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
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

            var hashed = PasswordHasher.Hash(request.Password);
            var user = new User(Guid.NewGuid(), request.Email, hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);

            return new RegisterResponse(user.Id, user.Email);
        }

        public async Task<User> ValidateCredentialsAsync(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email is required", nameof(request.Email));
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Password is required", nameof(request.Password));

            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null)
                throw new AuthenticationFailedException("Invalid credentials");

            if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
                throw new AuthenticationFailedException("Invalid credentials");

            return user;
        }

        public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                throw new ArgumentException("Current password is required", nameof(request.CurrentPassword));
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                throw new ArgumentException("New password is required", nameof(request.NewPassword));

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found", nameof(userId));

            if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
                throw new AuthenticationFailedException("Current password is incorrect");

            if (PasswordHasher.Verify(request.NewPassword, user.PasswordHash))
                throw new ArgumentException("New password must differ from current password", nameof(request.NewPassword));

            user.UpdatePassword(PasswordHasher.Hash(request.NewPassword));
            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("User {UserId} changed their password", userId);
        }
    }
}
