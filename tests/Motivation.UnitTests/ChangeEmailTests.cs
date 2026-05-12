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
    public class ChangeEmailTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly UserRepository _userRepository;
        private readonly AuthService _authService;

        public ChangeEmailTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_ChangeEmail_" + Guid.NewGuid())
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

        private async Task<User> CreateUserAsync(string email = "user@test.com", string password = "Pass123")
        {
            var hashed = PasswordHasher.Hash(password);
            var user = new User(Guid.NewGuid(), email, hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);
            return user;
        }

        // ── Happy path ────────────────────────────────────────────────────────

        [Fact]
        public async Task ChangeEmailAsync_ValidRequest_ReturnsWithoutException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "newemail@test.com"));

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task ChangeEmailAsync_ValidRequest_UpdatesEmail()
        {
            var user = await CreateUserAsync();

            await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "newemail@test.com"));

            var updated = await _userRepository.GetByIdAsync(user.Id);
            updated!.Email.Should().Be("newemail@test.com");
        }

        [Fact]
        public async Task ChangeEmailAsync_ValidRequest_NewEmailLookupWorks()
        {
            var user = await CreateUserAsync();

            await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "newemail@test.com"));

            var found = await _userRepository.GetByEmailAsync("newemail@test.com");
            found.Should().NotBeNull();
            found!.Id.Should().Be(user.Id);
        }

        [Fact]
        public async Task ChangeEmailAsync_ValidRequest_OldEmailLookupReturnsNull()
        {
            var user = await CreateUserAsync("old@test.com");

            await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "new@test.com"));

            var found = await _userRepository.GetByEmailAsync("old@test.com");
            found.Should().BeNull();
        }

        [Fact]
        public async Task ChangeEmailAsync_ValidRequest_CanLoginWithNewEmail()
        {
            var user = await CreateUserAsync("original@test.com", "Pass123");

            await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "updated@test.com"));

            Func<Task> act = async () =>
                await _authService.ValidateCredentialsAsync(new LoginRequest("updated@test.com", "Pass123"));

            await act.Should().NotThrowAsync();
        }

        // ── Wrong current password ────────────────────────────────────────────

        [Fact]
        public async Task ChangeEmailAsync_WrongPassword_ThrowsAuthenticationFailedException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("WrongPass", "new@test.com"));

            await act.Should().ThrowAsync<AuthenticationFailedException>()
                .WithMessage("*Current password is incorrect*");
        }

        // ── Same email ────────────────────────────────────────────────────────

        [Fact]
        public async Task ChangeEmailAsync_SameEmail_ThrowsArgumentException()
        {
            var user = await CreateUserAsync("user@test.com");

            Func<Task> act = async () =>
                await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "user@test.com"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*must differ*");
        }

        [Fact]
        public async Task ChangeEmailAsync_SameEmailDifferentCase_ThrowsArgumentException()
        {
            var user = await CreateUserAsync("user@test.com");

            Func<Task> act = async () =>
                await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "USER@TEST.COM"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*must differ*");
        }

        // ── Email already in use ──────────────────────────────────────────────

        [Fact]
        public async Task ChangeEmailAsync_EmailAlreadyInUse_ThrowsEmailAlreadyInUseException()
        {
            await CreateUserAsync("other@test.com");
            var user = await CreateUserAsync("user@test.com");

            Func<Task> act = async () =>
                await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "other@test.com"));

            await act.Should().ThrowAsync<EmailAlreadyInUseException>();
        }

        // ── User not found ────────────────────────────────────────────────────

        [Fact]
        public async Task ChangeEmailAsync_UserNotFound_ThrowsArgumentException()
        {
            var nonExistentId = Guid.NewGuid();

            Func<Task> act = async () =>
                await _authService.ChangeEmailAsync(nonExistentId, new ChangeEmailRequest("any", "new@test.com"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*User not found*");
        }

        // ── Empty / whitespace inputs ─────────────────────────────────────────

        [Fact]
        public async Task ChangeEmailAsync_EmptyPassword_ThrowsArgumentException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("", "new@test.com"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Current password*");
        }

        [Fact]
        public async Task ChangeEmailAsync_EmptyNewEmail_ThrowsArgumentException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", ""));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*New email*");
        }

        // ── Cache invalidation ────────────────────────────────────────────────

        [Fact]
        public async Task ChangeEmailAsync_AfterChange_CachedOldEmailIsInvalidated()
        {
            var user = await CreateUserAsync("cached@test.com");
            // Warm the old email cache
            await _userRepository.GetByEmailAsync("cached@test.com");

            await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "fresh@test.com"));

            // Old email should no longer resolve
            var found = await _userRepository.GetByEmailAsync("cached@test.com");
            found.Should().BeNull();
        }

        [Fact]
        public async Task ChangeEmailAsync_AfterChange_GetByIdReturnsUpdatedEmail()
        {
            var user = await CreateUserAsync();
            // Warm id cache
            await _userRepository.GetByIdAsync(user.Id);

            await _authService.ChangeEmailAsync(user.Id, new ChangeEmailRequest("Pass123", "refreshed@test.com"));

            var updated = await _userRepository.GetByIdAsync(user.Id);
            updated!.Email.Should().Be("refreshed@test.com");
        }
    }
}
