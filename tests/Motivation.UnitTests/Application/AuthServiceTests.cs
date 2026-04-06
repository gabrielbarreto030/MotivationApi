using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Motivation.Application.DTOs;
using Motivation.Application.Exceptions;
using Motivation.Application.Interfaces;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests.Application
{
    public class AuthServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly UserRepository _userRepository;
        private readonly AuthService _authService;
        private readonly FakeJwtService _fakeJwt;

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_AuthService_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _userRepository = new UserRepository(_context, _cache);
            _fakeJwt = new FakeJwtService();
            _authService = new AuthService(_userRepository, NullLogger<AuthService>.Instance, _fakeJwt);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        // ── RegisterAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task RegisterAsync_ValidRequest_ReturnsResponse()
        {
            var request = new RegisterRequest("user@test.com", "secret123");

            var result = await _authService.RegisterAsync(request);

            result.Should().NotBeNull();
            result.UserId.Should().NotBeEmpty();
            result.Email.Should().Be("user@test.com");
        }

        [Fact]
        public async Task RegisterAsync_ValidRequest_PersistsUser()
        {
            var request = new RegisterRequest("persist@test.com", "pass");

            var result = await _authService.RegisterAsync(request);

            var stored = await _userRepository.GetByEmailAsync("persist@test.com");
            stored.Should().NotBeNull();
            stored!.Id.Should().Be(result.UserId);
        }

        [Fact]
        public async Task RegisterAsync_ValidRequest_HashesPassword()
        {
            var request = new RegisterRequest("hash@test.com", "plaintext");

            await _authService.RegisterAsync(request);

            var stored = await _userRepository.GetByEmailAsync("hash@test.com");
            stored!.PasswordHash.Should().NotBe("plaintext");
            stored.PasswordHash.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task RegisterAsync_DuplicateEmail_ThrowsEmailAlreadyInUseException()
        {
            await _authService.RegisterAsync(new RegisterRequest("dup@test.com", "pass1"));

            Func<Task> act = async () =>
                await _authService.RegisterAsync(new RegisterRequest("dup@test.com", "pass2"));

            await act.Should().ThrowAsync<EmailAlreadyInUseException>();
        }

        [Fact]
        public async Task RegisterAsync_EmptyEmail_ThrowsArgumentException()
        {
            var request = new RegisterRequest("", "password");

            Func<Task> act = async () => await _authService.RegisterAsync(request);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Email*");
        }

        [Fact]
        public async Task RegisterAsync_WhitespaceEmail_ThrowsArgumentException()
        {
            var request = new RegisterRequest("   ", "password");

            Func<Task> act = async () => await _authService.RegisterAsync(request);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Email*");
        }

        [Fact]
        public async Task RegisterAsync_EmptyPassword_ThrowsArgumentException()
        {
            var request = new RegisterRequest("ok@test.com", "");

            Func<Task> act = async () => await _authService.RegisterAsync(request);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Password*");
        }

        [Fact]
        public async Task RegisterAsync_WhitespacePassword_ThrowsArgumentException()
        {
            var request = new RegisterRequest("ok@test.com", "   ");

            Func<Task> act = async () => await _authService.RegisterAsync(request);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Password*");
        }

        // ── LoginAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsLoginResponse()
        {
            var hashed = PasswordHasher.Hash("mypass");
            var user = new User(Guid.NewGuid(), "login@test.com", hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);

            var result = await _authService.LoginAsync(new LoginRequest("login@test.com", "mypass"));

            result.Should().NotBeNull();
            result.Email.Should().Be("login@test.com");
            result.UserId.Should().Be(user.Id);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsToken()
        {
            var hashed = PasswordHasher.Hash("secret");
            var user = new User(Guid.NewGuid(), "token@test.com", hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);

            var result = await _authService.LoginAsync(new LoginRequest("token@test.com", "secret"));

            result.Token.Should().Be("fake-token");
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ThrowsAuthenticationFailedException()
        {
            var hashed = PasswordHasher.Hash("correct");
            var user = new User(Guid.NewGuid(), "wrong@test.com", hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);

            Func<Task> act = async () =>
                await _authService.LoginAsync(new LoginRequest("wrong@test.com", "incorrect"));

            await act.Should().ThrowAsync<AuthenticationFailedException>();
        }

        [Fact]
        public async Task LoginAsync_UserNotFound_ThrowsAuthenticationFailedException()
        {
            Func<Task> act = async () =>
                await _authService.LoginAsync(new LoginRequest("nobody@test.com", "pass"));

            await act.Should().ThrowAsync<AuthenticationFailedException>();
        }

        [Fact]
        public async Task LoginAsync_EmptyEmail_ThrowsArgumentException()
        {
            Func<Task> act = async () =>
                await _authService.LoginAsync(new LoginRequest("", "pass"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Email*");
        }

        [Fact]
        public async Task LoginAsync_WhitespaceEmail_ThrowsArgumentException()
        {
            Func<Task> act = async () =>
                await _authService.LoginAsync(new LoginRequest("   ", "pass"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Email*");
        }

        [Fact]
        public async Task LoginAsync_EmptyPassword_ThrowsArgumentException()
        {
            Func<Task> act = async () =>
                await _authService.LoginAsync(new LoginRequest("user@test.com", ""));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Password*");
        }

        [Fact]
        public async Task LoginAsync_WhitespacePassword_ThrowsArgumentException()
        {
            Func<Task> act = async () =>
                await _authService.LoginAsync(new LoginRequest("user@test.com", "   "));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Password*");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private class FakeJwtService : IJwtService
        {
            public string GenerateToken(User user) => "fake-token";
        }
    }
}
