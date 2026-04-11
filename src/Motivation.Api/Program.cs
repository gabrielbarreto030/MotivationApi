using Serilog;
using Motivation.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Motivation.Domain.Interfaces;
using Motivation.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Motivation.Infrastructure.Services;
using Motivation.Application.Interfaces;
using Motivation.Application.Services;
using Motivation.Api.Services;
using Motivation.Infrastructure.HealthChecks;
using Motivation.Api.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// configure Serilog early so it can be used during startup
builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console()
    .ReadFrom.Configuration(ctx.Configuration));

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// health checks — custom checks with tags for readiness probes
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready", "db" })
    .AddCheck<MemoryCacheHealthCheck>("memory-cache", tags: new[] { "ready", "cache" });

// memory cache for quick queries
builder.Services.AddMemoryCache();

// http context accessor for ICurrentUserService
builder.Services.AddHttpContextAccessor();

// EF Core InMemory DbContext
builder.Services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase("MotivationDb"));

// repository registrations
builder.Services.AddScoped<IGoalRepository, GoalRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IStepRepository, StepRepository>();
builder.Services.AddScoped<IMotivationRepository, MotivationRepository>();

// application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<IStepService, StepService>();
builder.Services.AddScoped<IMotivationService, MotivationService>();
builder.Services.AddScoped<IDailyMessageService, DailyMessageService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev_secret_key_change_me";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "motivation";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "motivation";

builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        // Disable inbound claim mapping so "sub" stays as "sub"
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// add controllers
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// health endpoints — detailed JSON response for all probes
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedJsonResponse
});

// liveness: just confirms the app is running (no external dependencies)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedJsonResponse
});

// readiness: checks DB and cache before accepting traffic
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedJsonResponse
});

// map controllers
app.MapControllers();

app.Run();

public partial class Program { }
