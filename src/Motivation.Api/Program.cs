using Serilog;
using Motivation.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Motivation.Domain.Interfaces;
using Motivation.Infrastructure.Repositories;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    // additional dev-only middleware could go here
}

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

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
