using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Koot.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Koot.Api.Services;

public class JwtService : IJwtService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _key;
    private readonly TimeSpan _lifetime;

    public JwtService(IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");
        _issuer = section["Issuer"] ?? throw new InvalidOperationException("Missing Jwt:Issuer");
        _audience = section["Audience"] ?? throw new InvalidOperationException("Missing Jwt:Audience");

        var keyValue = section["Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));

        // Prefer ExpiryDays (KOOT-4 default = 7); fall back to legacy ExpiryMinutes if present.
        var days = section.GetValue<int?>("ExpiryDays");
        var minutes = section.GetValue<int?>("ExpiryMinutes");
        _lifetime = days is > 0
            ? TimeSpan.FromDays(days.Value)
            : TimeSpan.FromMinutes(minutes ?? 60);
    }

    public (string Token, DateTime ExpiresAt) IssueToken(User user)
    {
        var expiresAt = DateTime.UtcNow.Add(_lifetime);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("userId", user.Id.ToString()),
            new Claim("username", user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
        };

        var credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expiresAt);
    }
}
