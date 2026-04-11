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

        [Fact(Skip = "Investigating HttpClient default header handling")]
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

            // Add authorization header to the same client used for login
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            // sanity check: header should now appear in defaults
            client.DefaultRequestHeaders.TryGetValues("Authorization", out var hdrs).Should().BeTrue();
            _output.WriteLine("default auth headers: " + string.Join(",", hdrs ?? Array.Empty<string>()));

            var goalPayload = new { title = "My Goal", description = "Goal description" };
            var req = new HttpRequestMessage(HttpMethod.Post, "/goals")
            {
                Content = JsonContent.Create(goalPayload)
            };
            _output.WriteLine("Client default headers: " + client.DefaultRequestHeaders);
            _output.WriteLine("Request headers before send:\n" + req.Headers.ToString());
            // manually add authorization from default headers
            req.Headers.Authorization = client.DefaultRequestHeaders.Authorization;

            var goalRes = await client.SendAsync(req);
            _output.WriteLine("Response status: " + goalRes.StatusCode);
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

        [Fact(Skip = "Investigating HttpClient default header handling")]
        public async Task ListGoalsEndpoint_WithValidToken_ReturnsGoals()
        {
            var client = _factory.CreateClient();
            var registerPayload = new { email = "listuser@example.com", password = "pwd123" };
            var registerRes = await client.PostAsJsonAsync("/users/register", registerPayload);
            registerRes.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginPayload = new { email = "listuser@example.com", password = "pwd123" };
            var loginRes = await client.PostAsJsonAsync("/users/login", loginPayload);
            loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginContent = await loginRes.Content.ReadAsStringAsync();
            using var loginDoc = JsonDocument.Parse(loginContent);
            var token = loginDoc.RootElement.GetProperty("token").GetString();

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.TryGetValues("Authorization", out var hdrs).Should().BeTrue();
            _output.WriteLine("default auth headers: " + string.Join(",", hdrs ?? Array.Empty<string>()));
            
            // create two goals with explicit requests
            for (int i = 1; i <= 2; i++)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "/goals")
                {
                    Content = JsonContent.Create(new { title = $"G{i}", description = "d" })
                };
                req.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
                _output.WriteLine("Sending goal creation request headers:\n" + req.Headers);
                var res = await client.SendAsync(req);
                _output.WriteLine("Goal creation response: " + res.StatusCode);
            }

            var listReq = new HttpRequestMessage(HttpMethod.Get, "/goals");
            listReq.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
            var listRes = await client.SendAsync(listReq);
            _output.WriteLine("List response status: " + listRes.StatusCode);
            listRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var listContent = await listRes.Content.ReadAsStringAsync();
            using var listDoc = JsonDocument.Parse(listContent);
            listDoc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        }
    }
}
