using System.Text;
using Koot.Api.Data;
using Koot.Api.Hubs;
using Koot.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---- Serilog ----
builder.Host.UseSerilog((ctx, services, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext());

// ---- Services ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// CORS for the Vite dev server (and any other allowed origins from config)
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// EF Core (Pomelo MySQL/MariaDB)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Missing 'DefaultConnection' connection string.");

// Pin a server version so the host doesn't need to talk to the DB at startup
// (AutoDetect would block app boot if the DB is unreachable). Override if you
// upgrade the database major version.
var dbServerVersion = new MariaDbServerVersion(new Version(11, 4, 0));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, dbServerVersion));

// JWT bearer auth
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Allow JWT auth on SignalR connections (token sent via query string on WS handshake)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Application services
builder.Services.AddScoped<IJwtService, JwtService>();

// AutoMapper - scan this assembly for profiles
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// SignalR
builder.Services.AddSignalR();

// Health checks (basic)
builder.Services.AddHealthChecks();

var app = builder.Build();

// ---- Pipeline ----
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Do not force HTTPS redirect in containerized dev; uncomment for prod
// app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SignalR placeholder hub (real hub wired up in later tasks)
app.MapHub<GameHub>("/hubs/game");

// Health endpoints
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "koot-api",
    timestamp = DateTimeOffset.UtcNow
})).WithName("HealthCheck");

app.MapHealthChecks("/health");

app.Run();

// Expose Program for WebApplicationFactory<Program> in integration tests
public partial class Program { }
