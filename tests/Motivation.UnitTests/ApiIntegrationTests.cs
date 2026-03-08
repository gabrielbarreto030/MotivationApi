using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Motivation.Api;
using Xunit;

namespace Motivation.UnitTests
{
    public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public ApiIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task HealthEndpoint_ReturnsOk()
        {
            // arrange
            var client = _factory.CreateClient();

            // act
            var res = await client.GetAsync("/health");

            // assert
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SwaggerJson_IsAvailable()
        {
            var client = _factory.CreateClient();
            var res = await client.GetAsync("/swagger/v1/swagger.json");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RegisterEndpoint_CreatesUser()
        {
            var client = _factory.CreateClient();
            var payload = new { email = "new@user.com", password = "pwd" };
            var res = await client.PostAsJsonAsync("/users/register", payload);
            res.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task RegisterEndpoint_DuplicateEmail_ReturnsBadRequest()
        {
            var client = _factory.CreateClient();
            var payload = new { email = "dup@example.com", password = "pwd" };
            var first = await client.PostAsJsonAsync("/users/register", payload);
            first.StatusCode.Should().Be(HttpStatusCode.Created);

            var second = await client.PostAsJsonAsync("/users/register", payload);
            second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateGoalEndpoint_WithValidToken_CreatesGoal()
        {
            var client = _factory.CreateClient();

            // Register and login
            var registerPayload = new { email = "goaluser@example.com", password = "pwd123" };
            var registerRes = await client.PostAsJsonAsync("/users/register", registerPayload);
            registerRes.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginPayload = new { email = "goaluser@example.com", password = "pwd123" };
            var loginRes = await client.PostAsJsonAsync("/users/login", loginPayload);
            loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginContent = await loginRes.Content.ReadAsStringAsync();
            using var loginDoc = JsonDocument.Parse(loginContent);
            var token = loginDoc.RootElement.GetProperty("token").GetString();

            // Create goal with token
            var goalPayload = new { title = "My Goal", description = "Goal description" };
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var goalRes = await client.PostAsJsonAsync("/goals", goalPayload);
            goalRes.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task CreateGoalEndpoint_WithoutToken_Returns401()
        {
            var client = _factory.CreateClient();
            var goalPayload = new { title = "My Goal", description = "Goal description" };
            var res = await client.PostAsJsonAsync("/goals", goalPayload);
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
