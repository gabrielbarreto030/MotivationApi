using System.Net;
using System.Net.Http.Headers;
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
        private readonly Xunit.Abstractions.ITestOutputHelper _output;

        public ApiIntegrationTests(WebApplicationFactory<Program> factory, Xunit.Abstractions.ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
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
        public async Task RegisterEndpoint_DuplicateEmail_ReturnsConflict()
        {
            var client = _factory.CreateClient();
            var payload = new { email = "dup@example.com", password = "pwd" };
            var first = await client.PostAsJsonAsync("/users/register", payload);
            first.StatusCode.Should().Be(HttpStatusCode.Created);

            var second = await client.PostAsJsonAsync("/users/register", payload);
            second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task CreateGoalEndpoint_WithValidToken_CreatesGoal()
        {
            var client = _factory.CreateClient();
            var email = $"goaluser_{Guid.NewGuid():N}@example.com";

            var registerRes = await client.PostAsJsonAsync("/users/register", new { email, password = "pwd123" });
            registerRes.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginRes = await client.PostAsJsonAsync("/users/login", new { email, password = "pwd123" });
            loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginContent = await loginRes.Content.ReadAsStringAsync();
            using var loginDoc = JsonDocument.Parse(loginContent);
            var token = loginDoc.RootElement.GetProperty("token").GetString();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var goalRes = await client.PostAsJsonAsync("/goals", new { title = "My Goal", description = "Goal description" });
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

        [Fact]
        public async Task ListGoalsEndpoint_WithValidToken_ReturnsGoals()
        {
            var client = _factory.CreateClient();
            var email = $"listuser_{Guid.NewGuid():N}@example.com";

            await client.PostAsJsonAsync("/users/register", new { email, password = "pwd123" });
            var loginRes = await client.PostAsJsonAsync("/users/login", new { email, password = "pwd123" });
            loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginContent = await loginRes.Content.ReadAsStringAsync();
            using var loginDoc = JsonDocument.Parse(loginContent);
            var token = loginDoc.RootElement.GetProperty("token").GetString();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            await client.PostAsJsonAsync("/goals", new { title = "G1", description = "d" });
            await client.PostAsJsonAsync("/goals", new { title = "G2", description = "d" });

            var listRes = await client.GetAsync("/goals");
            listRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var listContent = await listRes.Content.ReadAsStringAsync();
            using var listDoc = JsonDocument.Parse(listContent);
            listDoc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        }
    }
}
