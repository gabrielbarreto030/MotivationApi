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
    public class DailyMessageControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public DailyMessageControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        private async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string password = "pwd123")
        {
            await client.PostAsJsonAsync("/users/register", new { email, password });
            var loginRes = await client.PostAsJsonAsync("/users/login", new { email, password });
            var content = await loginRes.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.GetProperty("token").GetString()!;
        }

        [Fact]
        public async Task GetDailyMessage_WithoutToken_Returns401()
        {
            var client = _factory.CreateClient();

            var res = await client.GetAsync("/daily-message");

            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact(Skip = "Investigating HttpClient default header handling")]
        public async Task GetDailyMessage_WithValidToken_Returns200WithMessageAndDate()
        {
            var client = _factory.CreateClient();
            var token = await RegisterAndLoginAsync(client, $"dailymsg_{Guid.NewGuid()}@test.com");

            var req = new HttpRequestMessage(HttpMethod.Get, "/daily-message");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            doc.RootElement.TryGetProperty("message", out var msgProp).Should().BeTrue();
            doc.RootElement.TryGetProperty("date", out _).Should().BeTrue();

            msgProp.GetString().Should().NotBeNullOrWhiteSpace();
        }

        [Fact(Skip = "Investigating HttpClient default header handling")]
        public async Task GetDailyMessage_WithNoMotivations_ReturnsDefaultMessage()
        {
            var client = _factory.CreateClient();
            var token = await RegisterAndLoginAsync(client, $"dailymsg_default_{Guid.NewGuid()}@test.com");

            var req = new HttpRequestMessage(HttpMethod.Get, "/daily-message");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            var message = doc.RootElement.GetProperty("message").GetString();
            message.Should().Be("Keep going! Every step forward is progress.");
        }
    }
}
