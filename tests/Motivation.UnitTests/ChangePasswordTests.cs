using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Motivation.Application.DTOs;
using Motivation.Application.Exceptions;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class ChangePasswordTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly UserRepository _userRepository;
        private readonly AuthService _authService;

        public ChangePasswordTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_ChangePassword_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _userRepository = new UserRepository(_context, _cache);
            _authService = new AuthService(_userRepository, NullLogger<AuthService>.Instance);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        private async Task<User> CreateUserAsync(string email = "user@test.com", string password = "OldPass123")
        {
            var hashed = PasswordHasher.Hash(password);
            var user = new User(Guid.NewGuid(), email, hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);
            return user;
        }

        // ── Happy path ────────────────────────────────────────────────────────

        [Fact]
        public async Task ChangePasswordAsync_ValidRequest_ReturnsWithoutException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("OldPass123", "NewPass456"));

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task ChangePasswordAsync_ValidRequest_UpdatesPasswordHash()
        {
            var user = await CreateUserAsync();

            await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("OldPass123", "NewPass456"));

            var updated = await _userRepository.GetByIdAsync(user.Id);
            PasswordHasher.Verify("NewPass456", updated!.PasswordHash).Should().BeTrue();
        }

        [Fact]
        public async Task ChangePasswordAsync_ValidRequest_OldPasswordNoLongerWorks()
        {
            var user = await CreateUserAsync();

            await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("OldPass123", "NewPass456"));

            var updated = await _userRepository.GetByIdAsync(user.Id);
            PasswordHasher.Verify("OldPass123", updated!.PasswordHash).Should().BeFalse();
        }

        [Fact]
        public async Task ChangePasswordAsync_ValidRequest_NewPasswordCanBeUsedToLogin()
        {
            var user = await CreateUserAsync("login@test.com", "OldPass123");

            await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("OldPass123", "NewPass456"));

            Func<Task> act = async () =>
                await _authService.ValidateCredentialsAsync(new LoginRequest("login@test.com", "NewPass456"));

            await act.Should().NotThrowAsync();
        }

        // ── Wrong current password ────────────────────────────────────────────

        [Fact]
        public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsAuthenticationFailedException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("WrongPass", "NewPass456"));

            await act.Should().ThrowAsync<AuthenticationFailedException>()
                .WithMessage("*Current password is incorrect*");
        }

        // ── Same password ─────────────────────────────────────────────────────

        [Fact]
        public async Task ChangePasswordAsync_SamePassword_ThrowsArgumentException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("OldPass123", "OldPass123"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*must differ*");
        }

        // ── User not found ────────────────────────────────────────────────────

        [Fact]
        public async Task ChangePasswordAsync_UserNotFound_ThrowsArgumentException()
        {
            var nonExistentId = Guid.NewGuid();

            Func<Task> act = async () =>
                await _authService.ChangePasswordAsync(nonExistentId, new ChangePasswordRequest("any", "new123"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*User not found*");
        }

        // ── Empty / whitespace inputs ─────────────────────────────────────────

        [Fact]
        public async Task ChangePasswordAsync_EmptyCurrentPassword_ThrowsArgumentException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("", "NewPass456"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Current password*");
        }

        [Fact]
        public async Task ChangePasswordAsync_WhitespaceCurrentPassword_ThrowsArgumentException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("   ", "NewPass456"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Current password*");
        }

        [Fact]
        public async Task ChangePasswordAsync_EmptyNewPassword_ThrowsArgumentException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("OldPass123", ""));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*New password*");
        }

        [Fact]
        public async Task ChangePasswordAsync_WhitespaceNewPassword_ThrowsArgumentException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("OldPass123", "   "));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*New password*");
        }

        // ── Cache invalidation ────────────────────────────────────────────────

        [Fact]
        public async Task ChangePasswordAsync_AfterChange_GetByIdReturnsUpdatedHash()
        {
            var user = await CreateUserAsync();
            // Warm cache
            await _userRepository.GetByIdAsync(user.Id);

            await _authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest("OldPass123", "FreshPass789"));

            var updated = await _userRepository.GetByIdAsync(user.Id);
            PasswordHasher.Verify("FreshPass789", updated!.PasswordHash).Should().BeTrue();
        }
    }
}
