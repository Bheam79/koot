using Koot.Api.Models;

namespace Koot.Api.Services;

public interface IJwtService
{
    /// <summary>Issues a signed JWT for the given user and returns the token + absolute expiry.</summary>
    (string Token, DateTime ExpiresAt) IssueToken(User user);
}
