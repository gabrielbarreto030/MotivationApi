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
using Motivation.Api.Middleware;
using Motivation.Api.Services;
using Microsoft.OpenApi.Models;
using System.Reflection;
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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Motivation API",
        Version = "v1",
        Description = "API de motivação pessoal para gerenciar metas (goals), passos (steps) e mensagens motivacionais diárias. " +
                      "Utilize JWT Bearer para autenticar endpoints protegidos.",
        Contact = new OpenApiContact
        {
            Name = "Motivation API",
            Email = "suporte@motivation.api"
        }
    });

    // JWT Bearer security definition — exibe botão Authorize no Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT obtido no endpoint POST /users/login.\nExemplo: Bearer {seu_token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // Inclui comentários XML gerados a partir das doc-strings dos controllers
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// health checks — basic + custom checks with tags for readiness probes
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
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IGoalProgressCalculator, GoalProgressCalculator>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<IStepService, StepService>();
builder.Services.AddScoped<IMotivationService, MotivationService>();
builder.Services.AddScoped<IDailyMessageService, DailyMessageService>();

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
        // Disable inbound claim mapping so "sub" stays as "sub" (controllers use User.FindFirst("sub"))
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

// global error handler — must be early in pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

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

// health endpoints — detailed JSON response for all, live and ready probes
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedJsonResponse
});

// liveness: apenas confirma que a aplicação está rodando (sem dependências externas)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedJsonResponse
});

// readiness: verifica DB e cache antes de aceitar tráfego
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedJsonResponse
});

// map controllers
app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
