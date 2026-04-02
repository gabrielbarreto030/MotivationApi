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

var builder = WebApplication.CreateBuilder(args);

// configure Serilog early so it can be used during startup
builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console()
    .ReadFrom.Configuration(ctx.Configuration));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// health check
builder.Services.AddHealthChecks();
// memory cache for quick queries
builder.Services.AddMemoryCache();

// EF Core InMemory DbContext
builder.Services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase("MotivationDb"));

// repository registrations
builder.Services.AddScoped<IGoalRepository, GoalRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IStepRepository, StepRepository>();
builder.Services.AddScoped<IMotivationRepository, MotivationRepository>();

// application services
builder.Services.AddScoped<Motivation.Application.Interfaces.IAuthService, Motivation.Application.Services.AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<Motivation.Application.Interfaces.IGoalService, Motivation.Application.Services.GoalService>();
builder.Services.AddScoped<Motivation.Application.Interfaces.IStepService, Motivation.Application.Services.StepService>();
builder.Services.AddScoped<Motivation.Application.Interfaces.IMotivationService, Motivation.Application.Services.MotivationService>();
builder.Services.AddScoped<Motivation.Application.Interfaces.IDailyMessageService, Motivation.Application.Services.DailyMessageService>();

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

// structured request logging with Serilog
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    // additional dev-only middleware could go here
}

app.UseAuthentication();
app.UseAuthorization();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

// health endpoint
app.MapHealthChecks("/health");

// map controllers
app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
