using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Koot.Api.Data;
using Koot.Api.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KootTests;

/// <summary>
/// Integration tests that fire N+1 requests through the full ASP.NET Core middleware
/// pipeline and assert that the (N+1)th request is rejected with 429 Too Many Requests.
///
/// Each test class creates its own <see cref="WebApplicationFactory{TEntryPoint}"/> via
/// <see cref="RateLimitFactory.Create"/> so the in-memory rate-limiter state and
/// the in-memory EF Core database are completely isolated between tests.
/// </summary>
public class LoginRateLimitTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public LoginRateLimitTests()
    {
        // Override the login permit limit to 2 so we can exhaust it with 3 requests.
        _factory = RateLimitFactory.Create(loginLimit: 2);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Login_Returns429_WhenPermitLimitExceeded()
    {
        var payload = new { email = "nobody@example.com", password = "wrong" };

        // Requests 1 and 2: rate limiter allows them; controller returns 401 (bad credentials).
        for (var i = 0; i < 2; i++)
        {
            var r = await _client.PostAsJsonAsync("/api/auth/login", payload);
            r.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                because: $"request {i + 1} is still within the 2-request permit limit");
        }

        // Request 3 (N+1): rate limiter should reject it.
        var limited = await _client.PostAsJsonAsync("/api/auth/login", payload);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            because: "the permit limit of 2 has been exhausted");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

public class RegisterRateLimitTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RegisterRateLimitTests()
    {
        _factory = RateLimitFactory.Create(registerLimit: 2);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Register_Returns429_WhenPermitLimitExceeded()
    {
        // Request 1 creates the user (200). Request 2 conflicts (409). Neither is 429.
        var payload = new { username = "bot", email = "bot@example.com", password = "password123" };

        for (var i = 0; i < 2; i++)
        {
            var r = await _client.PostAsJsonAsync("/api/auth/register", payload);
            r.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                because: $"request {i + 1} is still within the 2-request permit limit");
        }

        // Request 3 (N+1): rate limiter should reject it.
        var limited = await _client.PostAsJsonAsync("/api/auth/register", payload);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            because: "the permit limit of 2 has been exhausted");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

public class UploadRateLimitTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UploadRateLimitTests()
    {
        _factory = RateLimitFactory.Create(uploadLimit: 2);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Upload_Returns429_WhenPermitLimitExceeded()
    {
        // Send unauthenticated requests; rate limiter uses the IP as the partition key.
        // Requests 1 and 2: rate limiter allows → auth middleware returns 401 (not 429).
        // Request 3: rate limiter rejects before auth runs → 429.
        for (var i = 0; i < 2; i++)
        {
            using var form = new MultipartFormDataContent();
            var r = await _client.PostAsync("/api/uploads/image", form);
            r.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                because: $"request {i + 1} is still within the 2-request permit limit");
        }

        using var limitedForm = new MultipartFormDataContent();
        var limited = await _client.PostAsync("/api/uploads/image", limitedForm);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            because: "the permit limit of 2 has been exhausted");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

/// <summary>
/// Creates a <see cref="WebApplicationFactory{Program}"/> configured for rate-limit
/// integration tests:
/// <list type="bullet">
///   <item>Rate-limit thresholds overridden via <c>PostConfigure&lt;RateLimitingOptions&gt;</c>
///         (runs after the app's <c>Configure</c> call and is picked up at first request
///         because policies read <c>IOptions&lt;RateLimitingOptions&gt;</c> at request time).</item>
///   <item>MySQL <c>AppDbContext</c> replaced with an EF Core InMemory instance registered
///         directly via a scoped factory — avoids the dual-provider exception that arises when
///         <c>AddDbContext</c> is called twice for the same context type.</item>
/// </list>
/// </summary>
internal static class RateLimitFactory
{
    public static WebApplicationFactory<Program> Create(
        int loginLimit    = 10,
        int registerLimit = 5,
        int uploadLimit   = 60)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // ── Rate-limit threshold overrides ────────────────────────────
                // PostConfigure runs after the app's Configure<RateLimitingOptions> call.
                // The rate-limiter policies read IOptions<T> at request time, so they pick
                // up these overrides on the very first incoming request.
                services.PostConfigure<RateLimitingOptions>(opts =>
                {
                    opts.Login.PermitLimit    = loginLimit;
                    opts.Register.PermitLimit = registerLimit;
                    opts.Upload.PermitLimit   = uploadLimit;
                });

                // ── DbContext replacement ─────────────────────────────────────
                // Remove ALL DbContext-related service registrations added by Program.cs
                // (DbContextOptions<AppDbContext>, the non-generic DbContextOptions base,
                // and AppDbContext itself).  We then register AppDbContext directly using
                // an InMemory options instance.  Bypassing AddDbContext avoids registering
                // a second IDatabaseProvider (InMemory) alongside the MySQL one already
                // contributed by Program.cs — which would cause EF Core to throw
                // "Multiple providers configured".
                var typesToRemove = new[]
                {
                    typeof(DbContextOptions<AppDbContext>),
                    typeof(DbContextOptions),
                    typeof(AppDbContext),
                };

                var toRemove = services
                    .Where(d => typesToRemove.Contains(d.ServiceType))
                    .ToList();
                foreach (var d in toRemove) services.Remove(d);

                // Each factory gets a uniquely-named InMemory database so tests are
                // fully isolated from one another even if they run in parallel.
                var dbName = $"rl-test-{Guid.NewGuid():N}";
                var inMemoryOptions = new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase(dbName)
                    .Options;

                // Register as scoped so every request scope gets its own context
                // instance backed by the same shared InMemory database.
                services.AddScoped<AppDbContext>(_ => new AppDbContext(inMemoryOptions));
                services.AddScoped<DbContextOptions<AppDbContext>>(_ => inMemoryOptions);
                services.AddScoped<DbContextOptions>(_ => inMemoryOptions);
            });
        });
    }
}
