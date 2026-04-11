using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Motivation.Application.Exceptions;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class UserRegistrationTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly AuthService _authService;
        private readonly UserRepository _userRepository;

        public UserRegistrationTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Auth_" + Guid.NewGuid())
                .Options;
            _context = new AppDbContext(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _userRepository = new UserRepository(_context, _cache);
            _authService = new AuthService(_userRepository);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _cache?.Dispose();
        }

        [Fact]
        public async Task Register_NewUser_Succeeds()
        {
            var res = await _authService.RegisterAsync(new Motivation.Application.DTOs.RegisterRequest("new@user.com", "password"));
            res.UserId.Should().NotBeEmpty();
            res.Email.Should().Be("new@user.com");

            var stored = await _userRepository.GetByEmailAsync("new@user.com");
            stored.Should().NotBeNull();
            stored.Email.Should().Be("new@user.com");
            // password should be hashed
            stored.PasswordHash.Should().NotBe("password");
        }

        [Fact]
        public async Task Register_DuplicateEmail_Throws()
        {
            await _authService.RegisterAsync(new Motivation.Application.DTOs.RegisterRequest("dup@user.com", "pass"));

            Func<Task> act = async () => await _authService.RegisterAsync(new Motivation.Application.DTOs.RegisterRequest("dup@user.com", "other"));
            await act.Should().ThrowAsync<EmailAlreadyInUseException>();
        }
    }
}