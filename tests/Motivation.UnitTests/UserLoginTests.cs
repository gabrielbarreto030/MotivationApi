using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Motivation.Application.DTOs;
using Motivation.Application.Interfaces;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class UserLoginTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly UserRepository _userRepository;

        public UserLoginTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Login_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _userRepository = new UserRepository(_context, _cache);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsToken()
        {
            var password = "pass123";
            var hashed = PasswordHasher.Hash(password);
            var user = new User(Guid.NewGuid(), "login@user.com", hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);

            var fakeJwt = new FakeJwtService();
            var authService = new AuthService(_userRepository, fakeJwt);

            var res = await authService.LoginAsync(new LoginRequest("login@user.com", password));
            res.Token.Should().Be("fake-token");
            res.Email.Should().Be("login@user.com");
        }

        [Fact]
        public async Task Login_WithInvalidPassword_Throws()
        {
            var password = "pass123";
            var hashed = PasswordHasher.Hash(password);
            var user = new User(Guid.NewGuid(), "login2@user.com", hashed, DateTime.UtcNow);
            await _userRepository.AddAsync(user);

            var authService = new AuthService(_userRepository, new FakeJwtService());

            Func<Task> act = async () => await authService.LoginAsync(new LoginRequest("login2@user.com", "wrong"));
            await act.Should().ThrowAsync<Motivation.Application.Exceptions.AuthenticationFailedException>();
        }

        [Fact]
        public async Task Login_NonexistentUser_Throws()
        {
            var authService = new AuthService(_userRepository, new FakeJwtService());
            Func<Task> act = async () => await authService.LoginAsync(new LoginRequest("no@user.com", "x"));
            await act.Should().ThrowAsync<Motivation.Application.Exceptions.AuthenticationFailedException>();
        }

        private class FakeJwtService : IJwtService
        {
            public string GenerateToken(User user) => "fake-token";
        }
    }
}
