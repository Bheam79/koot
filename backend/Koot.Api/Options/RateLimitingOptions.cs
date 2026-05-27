namespace Koot.Api.Options;

/// <summary>
/// Configurable thresholds for the ASP.NET Core sliding-window rate limiter.
/// Bind via <c>appsettings.json</c> under the <c>RateLimiting</c> key.
/// </summary>
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Rate limit for POST /api/auth/login (per IP).</summary>
    public PolicyOptions Login { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Rate limit for POST /api/auth/register (per IP).</summary>
    public PolicyOptions Register { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    /// <summary>Rate limit for POST /api/uploads/image (per authenticated UserId, or per IP for anonymous).</summary>
    public PolicyOptions Upload { get; set; } = new() { PermitLimit = 60, WindowSeconds = 3600 };

    public class PolicyOptions
    {
        /// <summary>Maximum number of requests allowed in the window.</summary>
        public int PermitLimit { get; set; }

        /// <summary>Duration of the rate-limit window in seconds.</summary>
        public int WindowSeconds { get; set; }
    }
}
