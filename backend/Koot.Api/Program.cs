using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Koot.Api.Data;
using Koot.Api.Hubs;
using Koot.Api.Options;
using Koot.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
builder.Services.AddScoped<GameService>();
builder.Services.AddSingleton<GameStateService>();
builder.Services.AddHostedService<GameCleanupService>();

// AutoMapper - scan this assembly for profiles
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// SignalR
builder.Services.AddSignalR();

// Health checks (basic)
builder.Services.AddHealthChecks();

// ---- Rate limiting ----
// Thresholds are bound via IOptions<RateLimitingOptions> so tests can override them
// with PostConfigure<RateLimitingOptions> without restarting the host.
builder.Services.Configure<RateLimitingOptions>(
    builder.Configuration.GetSection(RateLimitingOptions.SectionName));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // POST /api/auth/login — sliding window per IP.
    // Options are resolved at request time so test overrides via PostConfigure are honoured.
    options.AddPolicy(RateLimitPolicies.Login, httpContext =>
    {
        var rlOpts = httpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitingOptions>>().Value.Login;
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: $"login:{ip}",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rlOpts.PermitLimit,
                Window = TimeSpan.FromSeconds(rlOpts.WindowSeconds),
                SegmentsPerWindow = 3,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    });

    // POST /api/auth/register — sliding window per IP
    options.AddPolicy(RateLimitPolicies.Register, httpContext =>
    {
        var rlOpts = httpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitingOptions>>().Value.Register;
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: $"register:{ip}",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rlOpts.PermitLimit,
                Window = TimeSpan.FromSeconds(rlOpts.WindowSeconds),
                SegmentsPerWindow = 3,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    });

    // POST /api/uploads/image — sliding window per authenticated UserId (IP fallback for anonymous).
    // UseAuthentication() runs before UseRateLimiter() in the pipeline so User is populated here.
    options.AddPolicy(RateLimitPolicies.Upload, httpContext =>
    {
        var rlOpts = httpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitingOptions>>().Value.Upload;
        var userId = httpContext.User.FindFirstValue("userId");
        var partitionKey = userId is not null
            ? $"upload:user:{userId}"
            : $"upload:ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rlOpts.PermitLimit,
                Window = TimeSpan.FromSeconds(rlOpts.WindowSeconds),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    });
});

var app = builder.Build();

// ---- Pipeline ----
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Do not force HTTPS redirect in containerized dev; uncomment for prod
// app.UseHttpsRedirection();

// Serve static files from wwwroot (used to expose uploaded images at /uploads/*)
app.UseStaticFiles();

app.UseCors("Frontend");

app.UseAuthentication();
// Rate limiter runs after authentication so httpContext.User is populated, allowing
// per-userId partitioning on the upload endpoint while falling back to IP for anonymous.
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

// Game hub
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
