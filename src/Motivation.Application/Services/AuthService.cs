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

        public AuthService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
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
    }
}