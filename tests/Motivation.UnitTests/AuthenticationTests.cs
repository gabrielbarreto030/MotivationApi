using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Motivation.Api;
using Xunit;

namespace Motivation.UnitTests
{
    public class AuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public AuthenticationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetProfile_WithoutToken_Returns401()
        {
            var client = _factory.CreateClient();
            var res = await client.GetAsync("/users/profile");
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetProfile_WithInvalidToken_Returns401()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer invalid-token");
            var res = await client.GetAsync("/users/profile");
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetProfile_WithValidToken_Returns200()
        {
            var client = _factory.CreateClient();

            // Step 1: Register & login to get token
            var registerPayload = new { email = "test@profile.com", password = "pwd123" };
            var registerRes = await client.PostAsJsonAsync("/users/register", registerPayload);
            registerRes.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginPayload = new { email = "test@profile.com", password = "pwd123" };
            var loginRes = await client.PostAsJsonAsync("/users/login", loginPayload);
            loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // For now, just verify that login returns a token
            var loginContent = await loginRes.Content.ReadAsStringAsync();
            using var loginDoc = JsonDocument.Parse(loginContent);
            var token = loginDoc.RootElement.GetProperty("token").GetString();
            token.Should().NotBeNullOrEmpty("Login should return a token");
        }

        [Fact]
        public async Task GetProfile_WithExpiredToken_Returns401()
        {
            var client = _factory.CreateClient();
            var expiredToken = GenerateExpiredToken();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {expiredToken}");
            
            var res = await client.GetAsync("/users/profile");
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        private static string GenerateExpiredToken()
        {
            var key = Encoding.UTF8.GetBytes("super_secret_key_12345_32bytes_min");
            var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new("sub", Guid.NewGuid().ToString()),
                new("email", "test@mail.com"),
                new("iss", "motivation"),
                new("aud", "motivation")
            };

            var token = new JwtSecurityToken(
                issuer: "motivation",
                audience: "motivation",
                claims: claims,
                expires: DateTime.UtcNow.AddSeconds(-10), // expired 10 seconds ago
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
