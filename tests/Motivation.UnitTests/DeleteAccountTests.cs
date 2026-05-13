using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Motivation.Application.Exceptions;
using Motivation.Application.Services;
using Motivation.Domain.Entities;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.Repositories;
using Xunit;

namespace Motivation.UnitTests
{
    public class DeleteAccountTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly UserRepository _userRepository;
        private readonly AuthService _authService;

        public DeleteAccountTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb_DeleteAccount_" + Guid.NewGuid())
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

        private async Task<(User user, Goal goal, Step step, Domain.Entities.Motivation motivation)> CreateUserWithDataAsync()
        {
            var user = await CreateUserAsync();

            var goal = new Goal(Guid.NewGuid(), user.Id, "My Goal", "Description", GoalStatus.Pending, DateTime.UtcNow);
            await _context.Goals.AddAsync(goal);

            var step = new Step(Guid.NewGuid(), goal.Id, "My Step");
            await _context.Steps.AddAsync(step);

            var motivation = new Domain.Entities.Motivation(Guid.NewGuid(), goal.Id, "Keep going!");
            await _context.Motivations.AddAsync(motivation);

            await _context.SaveChangesAsync();

            return (user, goal, step, motivation);
        }

        // ── Happy path ────────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAccountAsync_ValidPassword_CompletesWithoutException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.DeleteAccountAsync(user.Id, "Pass123");

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task DeleteAccountAsync_ValidPassword_RemovesUser()
        {
            var user = await CreateUserAsync();

            await _authService.DeleteAccountAsync(user.Id, "Pass123");

            var found = await _userRepository.GetByIdAsync(user.Id);
            found.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAccountAsync_ValidPassword_UserNotFoundByEmail()
        {
            var user = await CreateUserAsync("delete@test.com");

            await _authService.DeleteAccountAsync(user.Id, "Pass123");

            var found = await _userRepository.GetByEmailAsync("delete@test.com");
            found.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAccountAsync_WithGoalsStepsMotivations_RemovesAllRelatedData()
        {
            var (user, goal, step, motivation) = await CreateUserWithDataAsync();

            await _authService.DeleteAccountAsync(user.Id, "Pass123");

            var goalsLeft = await _context.Goals.Where(g => g.UserId == user.Id).ToListAsync();
            var stepsLeft = await _context.Steps.Where(s => s.GoalId == goal.Id).ToListAsync();
            var motivationsLeft = await _context.Motivations.Where(m => m.GoalId == goal.Id).ToListAsync();

            goalsLeft.Should().BeEmpty();
            stepsLeft.Should().BeEmpty();
            motivationsLeft.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteAccountAsync_WithGoals_RemovesGoals()
        {
            var (user, goal, _, _) = await CreateUserWithDataAsync();

            await _authService.DeleteAccountAsync(user.Id, "Pass123");

            var goalsLeft = await _context.Goals.Where(g => g.UserId == user.Id).ToListAsync();
            goalsLeft.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteAccountAsync_WithSteps_RemovesSteps()
        {
            var (user, goal, step, _) = await CreateUserWithDataAsync();

            await _authService.DeleteAccountAsync(user.Id, "Pass123");

            var stepsLeft = await _context.Steps.Where(s => s.GoalId == goal.Id).ToListAsync();
            stepsLeft.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteAccountAsync_WithMotivations_RemovesMotivations()
        {
            var (user, goal, _, motivation) = await CreateUserWithDataAsync();

            await _authService.DeleteAccountAsync(user.Id, "Pass123");

            var motivationsLeft = await _context.Motivations.Where(m => m.GoalId == goal.Id).ToListAsync();
            motivationsLeft.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteAccountAsync_UserWithNoData_Succeeds()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.DeleteAccountAsync(user.Id, "Pass123");

            await act.Should().NotThrowAsync();
        }

        // ── Wrong password ────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAccountAsync_WrongPassword_ThrowsAuthenticationFailedException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.DeleteAccountAsync(user.Id, "WrongPass");

            await act.Should().ThrowAsync<AuthenticationFailedException>()
                .WithMessage("*Password is incorrect*");
        }

        [Fact]
        public async Task DeleteAccountAsync_WrongPassword_DoesNotDeleteUser()
        {
            var user = await CreateUserAsync();

            try { await _authService.DeleteAccountAsync(user.Id, "WrongPass"); } catch { }

            var found = await _userRepository.GetByIdAsync(user.Id);
            found.Should().NotBeNull();
        }

        // ── Validation errors ─────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAccountAsync_EmptyPassword_ThrowsArgumentException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.DeleteAccountAsync(user.Id, "");

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Password is required*");
        }

        [Fact]
        public async Task DeleteAccountAsync_WhitespacePassword_ThrowsArgumentException()
        {
            var user = await CreateUserAsync();

            Func<Task> act = async () =>
                await _authService.DeleteAccountAsync(user.Id, "   ");

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Password is required*");
        }

        // ── User not found ────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAccountAsync_UserNotFound_ThrowsArgumentException()
        {
            Func<Task> act = async () =>
                await _authService.DeleteAccountAsync(Guid.NewGuid(), "Pass123");

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*User not found*");
        }

        // ── Cache invalidation ────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAccountAsync_AfterDelete_CacheByIdIsInvalidated()
        {
            var user = await CreateUserAsync();
            // Warm the cache
            await _userRepository.GetByIdAsync(user.Id);

            await _authService.DeleteAccountAsync(user.Id, "Pass123");

            var found = await _userRepository.GetByIdAsync(user.Id);
            found.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAccountAsync_AfterDelete_CacheByEmailIsInvalidated()
        {
            var user = await CreateUserAsync("cached@test.com");
            // Warm the cache
            await _userRepository.GetByEmailAsync("cached@test.com");

            await _authService.DeleteAccountAsync(user.Id, "Pass123");

            var found = await _userRepository.GetByEmailAsync("cached@test.com");
            found.Should().BeNull();
        }

        // ── Isolation ─────────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAccountAsync_OnlyDeletesTargetUserData()
        {
            var user1 = await CreateUserAsync("user1@test.com");
            var user2 = await CreateUserAsync("user2@test.com");

            var goal2 = new Goal(Guid.NewGuid(), user2.Id, "User2 Goal", "Desc", GoalStatus.Pending, DateTime.UtcNow);
            await _context.Goals.AddAsync(goal2);
            await _context.SaveChangesAsync();

            await _authService.DeleteAccountAsync(user1.Id, "Pass123");

            var user2Still = await _userRepository.GetByIdAsync(user2.Id);
            user2Still.Should().NotBeNull();

            var user2Goals = await _context.Goals.Where(g => g.UserId == user2.Id).ToListAsync();
            user2Goals.Should().HaveCount(1);
        }
    }
}
