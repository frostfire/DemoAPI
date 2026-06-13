using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CaseFlow.Contracts.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CaseFlow.Api.Auth;

public sealed class DemoTokenService(IOptions<JwtOptions> jwtOptions, TimeProvider clock)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public DemoTokenResponse IssueToken(IReadOnlyList<string> roles, string? organizationId, string? userId)
    {
        var options = jwtOptions.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.Add(TokenLifetime);

        var org = string.IsNullOrWhiteSpace(organizationId) ? "org_demo_001" : organizationId;
        var user = string.IsNullOrWhiteSpace(userId)
            ? $"user_{Guid.CreateVersion7():N}"[..16]
            : userId;

        List<Claim> claims =
        [
            new("sub", user),
            new("org", org),
            new("jti", Guid.CreateVersion7().ToString("N")),
        ];
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new DemoTokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            expiresAt,
            user,
            org,
            roles);
    }
}
