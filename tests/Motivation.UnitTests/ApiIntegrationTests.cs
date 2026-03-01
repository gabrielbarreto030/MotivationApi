using System.Net;
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
    }
}
