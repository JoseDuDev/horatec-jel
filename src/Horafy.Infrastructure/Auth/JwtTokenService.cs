using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Horafy.Infrastructure.Auth;

internal sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _opts = options.Value;

    private const string TokenTypeRefresh = "refresh";
    private const string ClaimPermissions = "permissions";
    private const string ClaimTenantId    = "tenant_id";
    private const string ClaimTokenType   = "token_type";

    public TokenPair GenerateTokens(User user)
    {
        var key     = BuildKey();
        var now     = DateTimeOffset.UtcNow;
        var handler = new JwtSecurityTokenHandler();

        // ── Access token ──────────────────────────────────────────────
        var accessExpires = now.AddMinutes(_opts.ExpirationMinutes);
        var accessClaims  = BuildAccessClaims(user);

        var accessDescriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(accessClaims),
            Expires            = accessExpires.UtcDateTime,
            Issuer             = _opts.Issuer,
            Audience           = _opts.Audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var accessToken = handler.WriteToken(handler.CreateToken(accessDescriptor));

        // ── Refresh token (stateless, 7 dias) ─────────────────────────
        var refreshExpires = now.AddDays(_opts.RefreshTokenExpirationDays);
        var refreshClaims  = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTokenType, TokenTypeRefresh)
        };

        var refreshDescriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(refreshClaims),
            Expires            = refreshExpires.UtcDateTime,
            Issuer             = _opts.Issuer,
            Audience           = _opts.Audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var refreshToken = handler.WriteToken(handler.CreateToken(refreshDescriptor));

        return new TokenPair(accessToken, refreshToken, accessExpires, refreshExpires);
    }

    public ServiceToken GenerateIntegrationToken(Guid tenantId, string? scopes = null)
    {
        var key     = BuildKey();
        var now     = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_opts.IntegrationTokenExpirationMinutes);
        var handler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, $"integration:{tenantId}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, UserRole.TenantStaff.ToString()),
            new(ClaimTenantId, tenantId.ToString()),
            new("source", "integration"),
        };

        if (!string.IsNullOrWhiteSpace(scopes))
            claims.Add(new Claim("scope", scopes));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Expires            = expires.UtcDateTime,
            Issuer             = _opts.Issuer,
            Audience           = _opts.Audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = handler.WriteToken(handler.CreateToken(descriptor));
        return new ServiceToken(token, expires);
    }

    public ClaimsPrincipal? ValidateRefreshToken(string refreshToken)
    {
        var handler    = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey        = BuildKey(),
            ValidateIssuer          = true,
            ValidIssuer             = _opts.Issuer,
            ValidateAudience        = true,
            ValidAudience           = _opts.Audience,
            ValidateLifetime        = true,
            ClockSkew               = TimeSpan.Zero
        };

        try
        {
            var principal = handler.ValidateToken(refreshToken, parameters, out _);

            // Garante que é realmente um refresh token
            var tokenType = principal.FindFirstValue(ClaimTokenType);
            if (tokenType != TokenTypeRefresh)
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private SymmetricSecurityKey BuildKey() =>
        new(Encoding.UTF8.GetBytes(_opts.Secret));

    private static IEnumerable<Claim> BuildAccessClaims(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(ClaimTypes.Role,               user.Role.ToString()),
        };

        if (!string.IsNullOrEmpty(user.Name))
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.Name));

        if (user.TenantId.HasValue)
            claims.Add(new Claim(ClaimTenantId, user.TenantId.Value.ToString()));

        var permissionsValue = string.Join(',', user.Permissions.Select(p => p.ToString()));
        claims.Add(new Claim(ClaimPermissions, permissionsValue));

        return claims;
    }
}
