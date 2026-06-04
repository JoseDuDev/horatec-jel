using FluentAssertions;
using Horafy.Domain.Entities.Users;
using Horafy.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace Horafy.Infrastructure.Tests.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(string? secret = null)
    {
        var opts = new JwtOptions
        {
            Secret                    = secret ?? "test-secret-key-minimo-32-chars-aqui!",
            Issuer                    = "horafy-test",
            Audience                  = "horafy-test-clients",
            ExpirationMinutes         = 60,
            RefreshTokenExpirationDays = 7
        };
        return new JwtTokenService(Options.Create(opts));
    }

    // ── GenerateTokens ────────────────────────────────────────────────
    [Fact]
    public void GenerateTokens_ReturnsNonEmptyTokens()
    {
        var user    = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);
        var service = CreateService();

        var tokens = service.GenerateTokens(user);

        tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        tokens.AccessToken.Should().NotBe(tokens.RefreshToken);
    }

    [Fact]
    public void GenerateTokens_AccessTokenExpiresInFuture()
    {
        var user    = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);
        var service = CreateService();

        var tokens = service.GenerateTokens(user);

        tokens.AccessTokenExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void GenerateTokens_RefreshTokenExpiresAfterAccessToken()
    {
        var user    = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);
        var service = CreateService();

        var tokens = service.GenerateTokens(user);

        tokens.RefreshTokenExpiresAt.Should().BeAfter(tokens.AccessTokenExpiresAt);
    }

    // ── ValidateRefreshToken ──────────────────────────────────────────
    [Fact]
    public void ValidateRefreshToken_ValidToken_ReturnsPrincipal()
    {
        var user    = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);
        var service = CreateService();

        var tokens    = service.GenerateTokens(user);
        var principal = service.ValidateRefreshToken(tokens.RefreshToken);

        principal.Should().NotBeNull();
        principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value
                  .Should().Be(user.Id.ToString());
    }

    [Fact]
    public void ValidateRefreshToken_AccessTokenAsRefreshToken_ReturnsNull()
    {
        var user    = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);
        var service = CreateService();

        var tokens    = service.GenerateTokens(user);
        // Usa o access token onde esperava-se o refresh token
        var principal = service.ValidateRefreshToken(tokens.AccessToken);

        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateRefreshToken_InvalidToken_ReturnsNull()
    {
        var service   = CreateService();
        var principal = service.ValidateRefreshToken("token.invalido.aqui");

        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateRefreshToken_TokenFromDifferentSecret_ReturnsNull()
    {
        var user     = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);
        var service1 = CreateService("chave-secreta-um-32-chars-abcdefgh!");
        var service2 = CreateService("chave-secreta-dois-32-chars-abcdef!");

        var tokens    = service1.GenerateTokens(user);
        var principal = service2.ValidateRefreshToken(tokens.RefreshToken);

        principal.Should().BeNull();
    }

    // ── Claims do access token ────────────────────────────────────────
    [Fact]
    public void GenerateTokens_AccessToken_ContainsTenantIdClaim()
    {
        var tenantId = Guid.NewGuid();
        var user     = User.CreateWithEmail("jose@gmail.com", "hash", "José", tenantId, UserRole.TenantOwner);
        var service  = CreateService();

        var tokens = service.GenerateTokens(user);

        var handler   = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt       = handler.ReadJwtToken(tokens.AccessToken);
        var tenantClaim = jwt.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;

        tenantClaim.Should().Be(tenantId.ToString());
    }
}
