using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Motivation.Api;
using Motivation.Infrastructure.Db;
using Motivation.Infrastructure.HealthChecks;
using Xunit;

namespace Motivation.UnitTests;

public class HealthChecksTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthChecksTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOkWithJsonBody()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        doc.RootElement.TryGetProperty("totalDurationMs", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("checks", out var checks).Should().BeTrue();
        checks.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HealthLiveEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        // liveness has no checks
        doc.RootElement.GetProperty("checks").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task HealthReadyEndpoint_ReturnsOkWithDatabaseAndCacheChecks()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

        var checks = doc.RootElement.GetProperty("checks").EnumerateArray().ToList();
        checks.Should().HaveCount(2);

        var names = checks.Select(c => c.GetProperty("name").GetString()).ToList();
        names.Should().Contain("database");
        names.Should().Contain("memory-cache");

        foreach (var check in checks)
        {
            check.GetProperty("status").GetString().Should().Be("Healthy");
        }
    }

    [Fact]
    public async Task HealthEndpoint_IncludesDatabaseCheck()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var checks = doc.RootElement.GetProperty("checks").EnumerateArray().ToList();
        checks.Should().Contain(c => c.GetProperty("name").GetString() == "database");
    }

    [Fact]
    public async Task HealthEndpoint_IncludesMemoryCacheCheck()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var checks = doc.RootElement.GetProperty("checks").EnumerateArray().ToList();
        checks.Should().Contain(c => c.GetProperty("name").GetString() == "memory-cache");
    }
}

public class DatabaseHealthCheckUnitTests
{
    [Fact]
    public async Task DatabaseHealthCheck_WhenCanConnect_ReturnsHealthy()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("health_check_test_db")
            .Options;

        using var dbContext = new AppDbContext(options);
        var check = new DatabaseHealthCheck(dbContext);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Database is accessible");
    }
}

public class MemoryCacheHealthCheckUnitTests
{
    [Fact]
    public async Task MemoryCacheHealthCheck_WithOperationalCache_ReturnsHealthy()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var check = new MemoryCacheHealthCheck(cache);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("MemoryCache is operational");
    }
}
