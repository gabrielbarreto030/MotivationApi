using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Motivation.Api;
using Motivation.Infrastructure.Db;
using Xunit;

namespace Motivation.UnitTests.Integration;

// ── Custom factory with isolated in-memory DB per class instance ──────────────

public class MotivationApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "IntTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContextOptions<AppDbContext> to avoid shared state
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add fresh isolated in-memory database
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}

// ── Integration tests ─────────────────────────────────────────────────────────

public class ApiFlowIntegrationTests : IClassFixture<MotivationApiFactory>
{
    private readonly MotivationApiFactory _factory;

    public ApiFlowIntegrationTests(MotivationApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>
    /// Creates a fresh HttpClient already authenticated via register + login.
    /// The client's DefaultRequestHeaders.Authorization is set to the JWT token.
    /// </summary>
    private async Task<(HttpClient client, string token)> AuthenticatedClientAsync(string? label = null)
    {
        var client = CreateClient();
        var email = $"user_{label ?? Guid.NewGuid().ToString("N")}_{Guid.NewGuid():N}@test.com";
        const string password = "TestPass123";

        await client.PostAsJsonAsync("/users/register", new { email, password });
        var loginRes = await client.PostAsJsonAsync("/users/login", new { email, password });
        var content = await loginRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var token = doc.RootElement.GetProperty("token").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Auth_Register_ReturnsCreated()
    {
        var client = CreateClient();
        var email = $"reg_{Guid.NewGuid():N}@test.com";
        var res = await client.PostAsJsonAsync("/users/register", new { email, password = "pwd" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Auth_Register_DuplicateEmail_ReturnsConflict()
    {
        var client = CreateClient();
        var email = $"dup_{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/users/register", new { email, password = "pwd" });
        var res = await client.PostAsJsonAsync("/users/register", new { email, password = "pwd" });
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Auth_Login_WithValidCredentials_ReturnsToken()
    {
        var client = CreateClient();
        var email = $"login_{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/users/register", new { email, password = "pwd123" });

        var res = await client.PostAsJsonAsync("/users/login", new { email, password = "pwd123" });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.TryGetProperty("userId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Auth_Login_WithWrongPassword_Returns401()
    {
        var client = CreateClient();
        var email = $"badpwd_{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/users/register", new { email, password = "correct" });

        var res = await client.PostAsJsonAsync("/users/login", new { email, password = "wrong" });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Auth_GetProfile_WithValidToken_ReturnsUserInfo()
    {
        var (client, _) = await AuthenticatedClientAsync("profile");
        var res = await client.GetAsync("/users/profile");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("userId");
    }

    [Fact]
    public async Task Auth_GetProfile_WithoutToken_Returns401()
    {
        var res = await CreateClient().GetAsync("/users/profile");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Goals ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Goals_Create_WithAuth_ReturnsCreated()
    {
        var (client, _) = await AuthenticatedClientAsync("goals_create");
        var res = await client.PostAsJsonAsync("/goals", new { title = "Learn DDD", description = "Study DDD" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.TryGetProperty("id", out _).Should().BeTrue();
        doc.RootElement.GetProperty("title").GetString().Should().Be("Learn DDD");
    }

    [Fact]
    public async Task Goals_Create_WithoutAuth_Returns401()
    {
        var res = await CreateClient().PostAsJsonAsync("/goals", new { title = "Goal", description = "d" });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Goals_List_WithAuth_ReturnsGoalsList()
    {
        var (client, _) = await AuthenticatedClientAsync("goals_list");
        await client.PostAsJsonAsync("/goals", new { title = "G1", description = "d1" });
        await client.PostAsJsonAsync("/goals", new { title = "G2", description = "d2" });

        var res = await client.GetAsync("/goals");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Goals_Update_WithAuth_ReturnsUpdated()
    {
        var (client, _) = await AuthenticatedClientAsync("goals_update");
        var createRes = await client.PostAsJsonAsync("/goals", new { title = "Old Title", description = "d" });
        var goalId = GetId(await createRes.Content.ReadAsStringAsync());

        var updateRes = await client.PutAsJsonAsync($"/goals/{goalId}", new { title = "New Title", description = "updated" });

        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateContent = await updateRes.Content.ReadAsStringAsync();
        updateContent.Should().Contain("New Title");
    }

    [Fact]
    public async Task Goals_Delete_WithAuth_ReturnsNoContent()
    {
        var (client, _) = await AuthenticatedClientAsync("goals_delete");
        var createRes = await client.PostAsJsonAsync("/goals", new { title = "To Delete", description = "d" });
        var goalId = GetId(await createRes.Content.ReadAsStringAsync());

        var deleteRes = await client.DeleteAsync($"/goals/{goalId}");

        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Goals_GetProgress_WithNoSteps_Returns0Percent()
    {
        var (client, _) = await AuthenticatedClientAsync("goals_progress");
        var createRes = await client.PostAsJsonAsync("/goals", new { title = "Progress Goal", description = "d" });
        var goalId = GetId(await createRes.Content.ReadAsStringAsync());

        var progressRes = await client.GetAsync($"/goals/{goalId}/progress");

        progressRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await progressRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.TryGetProperty("progressPercentage", out var pct).Should().BeTrue();
        pct.GetDouble().Should().Be(0);
    }

    // ── Steps ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Steps_Create_WithAuth_ReturnsCreated()
    {
        var (client, _) = await AuthenticatedClientAsync("steps_create");
        var goalId = await CreateGoalAsync(client, "Goal for steps");

        var res = await client.PostAsJsonAsync($"/goals/{goalId}/steps", new { title = "First step" });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("title").GetString().Should().Be("First step");
        doc.RootElement.GetProperty("isCompleted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Steps_List_WithAuth_ReturnsSteps()
    {
        var (client, _) = await AuthenticatedClientAsync("steps_list");
        var goalId = await CreateGoalAsync(client, "Goal for step listing");
        await client.PostAsJsonAsync($"/goals/{goalId}/steps", new { title = "Step 1" });
        await client.PostAsJsonAsync($"/goals/{goalId}/steps", new { title = "Step 2" });

        var res = await client.GetAsync($"/goals/{goalId}/steps");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Steps_MarkComplete_WithAuth_SetsIsCompletedTrue()
    {
        var (client, _) = await AuthenticatedClientAsync("steps_complete");
        var goalId = await CreateGoalAsync(client, "Goal for completing steps");
        var stepRes = await client.PostAsJsonAsync($"/goals/{goalId}/steps", new { title = "Complete me" });
        var stepId = GetId(await stepRes.Content.ReadAsStringAsync());

        var completeRes = await client.PutAsync($"/goals/{goalId}/steps/{stepId}/complete", null);

        completeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await completeRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("isCompleted").GetBoolean().Should().BeTrue();
    }

    // ── Motivations ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Motivations_Add_WithAuth_ReturnsCreated()
    {
        var (client, _) = await AuthenticatedClientAsync("motivations_add");
        var goalId = await CreateGoalAsync(client, "Goal for motivation");

        var res = await client.PostAsJsonAsync($"/goals/{goalId}/motivations", new { text = "You can do it!" });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("text").GetString().Should().Be("You can do it!");
    }

    [Fact]
    public async Task Motivations_Remove_WithAuth_ReturnsNoContent()
    {
        var (client, _) = await AuthenticatedClientAsync("motivations_remove");
        var goalId = await CreateGoalAsync(client, "Goal for removing motivation");
        var addRes = await client.PostAsJsonAsync($"/goals/{goalId}/motivations", new { text = "Temp motivation" });
        var motId = GetId(await addRes.Content.ReadAsStringAsync());

        var removeRes = await client.DeleteAsync($"/goals/{goalId}/motivations/{motId}");

        removeRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Daily Message ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DailyMessage_WithoutAuth_Returns401()
    {
        var res = await CreateClient().GetAsync("/daily-message");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DailyMessage_WithAuth_NoMotivations_ReturnsDefaultMessage()
    {
        var (client, _) = await AuthenticatedClientAsync("daily_default");

        var res = await client.GetAsync("/daily-message");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("message").GetString()
            .Should().Be("Keep going! Every step forward is progress.");
        doc.RootElement.TryGetProperty("date", out _).Should().BeTrue();
    }

    [Fact]
    public async Task DailyMessage_WithAuth_WithMotivations_ReturnsMotivationMessage()
    {
        var (client, _) = await AuthenticatedClientAsync("daily_motivated");
        var goalId = await CreateGoalAsync(client, "Motivating goal");
        await client.PostAsJsonAsync($"/goals/{goalId}/motivations", new { text = "Believe in yourself!" });

        var res = await client.GetAsync("/daily-message");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    // ── Infrastructure ────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var res = await CreateClient().GetAsync("/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SwaggerJson_IsAvailable()
    {
        var res = await CreateClient().GetAsync("/swagger/v1/swagger.json");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Full Workflow ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FullWorkflow_UserCompletesGoalWithStepsAndMotivations()
    {
        // Register + login
        var (client, _) = await AuthenticatedClientAsync("workflow");

        // Create a goal
        var goalRes = await client.PostAsJsonAsync("/goals", new { title = "Learn SOLID", description = "Study SOLID" });
        goalRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var goalId = GetId(await goalRes.Content.ReadAsStringAsync());

        // Add two steps
        var step1Res = await client.PostAsJsonAsync($"/goals/{goalId}/steps", new { title = "Read Clean Code" });
        step1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var step1Id = GetId(await step1Res.Content.ReadAsStringAsync());

        var step2Res = await client.PostAsJsonAsync($"/goals/{goalId}/steps", new { title = "Practice SOLID" });
        step2Res.StatusCode.Should().Be(HttpStatusCode.Created);

        // Add a motivation
        var motRes = await client.PostAsJsonAsync($"/goals/{goalId}/motivations", new { text = "You are making progress!" });
        motRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Progress should be 0% initially
        var progress1Res = await client.GetAsync($"/goals/{goalId}/progress");
        progress1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var progress1Doc = JsonDocument.Parse(await progress1Res.Content.ReadAsStringAsync());
        progress1Doc.RootElement.GetProperty("progressPercentage").GetDouble().Should().Be(0);

        // Mark step 1 complete
        var completeRes = await client.PutAsync($"/goals/{goalId}/steps/{step1Id}/complete", null);
        completeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Progress should be 50%
        var progress2Res = await client.GetAsync($"/goals/{goalId}/progress");
        using var progress2Doc = JsonDocument.Parse(await progress2Res.Content.ReadAsStringAsync());
        progress2Doc.RootElement.GetProperty("progressPercentage").GetDouble().Should().Be(50);

        // Daily message should return the motivation text
        var dailyRes = await client.GetAsync("/daily-message");
        dailyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        using var dailyDoc = JsonDocument.Parse(await dailyRes.Content.ReadAsStringAsync());
        dailyDoc.RootElement.GetProperty("message").GetString().Should().NotBeNullOrEmpty();

        // Delete the goal
        var deleteRes = await client.DeleteAsync($"/goals/{goalId}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Goals list should be empty
        var listRes = await client.GetAsync("/goals");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        using var listDoc = JsonDocument.Parse(await listRes.Content.ReadAsStringAsync());
        listDoc.RootElement.GetArrayLength().Should().Be(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateGoalAsync(HttpClient client, string title)
    {
        var res = await client.PostAsJsonAsync("/goals", new { title, description = "d" });
        return GetId(await res.Content.ReadAsStringAsync());
    }
}
