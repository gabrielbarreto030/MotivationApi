using System.Net;
using System.Net.Http.Json;
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
    }
}
