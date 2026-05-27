namespace Koot.Api.Options;

/// <summary>Named rate-limiting policy identifiers referenced by controllers and Program.cs.</summary>
public static class RateLimitPolicies
{
    public const string Login    = "login-limit";
    public const string Register = "register-limit";
    public const string Upload   = "upload-limit";
}
