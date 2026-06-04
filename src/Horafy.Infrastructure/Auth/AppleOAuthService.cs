using Horafy.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace Horafy.Infrastructure.Auth;

/// <summary>
/// Valida Apple Identity Tokens buscando as chaves públicas da Apple
/// em https://appleid.apple.com/auth/keys e verificando assinatura + claims.
/// </summary>
internal sealed class AppleOAuthService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<AppleOAuthService> logger) : IAppleOAuthService
{
    private const string AppleJwksUrl  = "https://appleid.apple.com/auth/keys";
    private const string AppleIssuer   = "https://appleid.apple.com";

    private readonly string? _clientId = configuration["Apple:ClientId"];

    public async Task<OAuthUserInfo?> ValidateAsync(
        string identityToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var keys = await FetchApplePublicKeysAsync(cancellationToken);
            if (keys is null) return null;

            var handler    = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys        = keys,
                ValidateIssuer           = true,
                ValidIssuer              = AppleIssuer,
                ValidateAudience         = !string.IsNullOrWhiteSpace(_clientId),
                ValidAudience            = _clientId,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.FromMinutes(5)
            };

            var principal = handler.ValidateToken(identityToken, parameters, out _);

            var sub   = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                     ?? principal.FindFirst("email")?.Value;

            if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(email))
            {
                logger.LogWarning("Apple token válido mas sem sub/email.");
                return null;
            }

            return new OAuthUserInfo(ProviderId: sub, Email: email, Name: null, AvatarUrl: null);
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning(ex, "Apple identity token inválido.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao validar Apple identity token.");
            return null;
        }
    }

    private async Task<IEnumerable<SecurityKey>?> FetchApplePublicKeysAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var client   = httpClientFactory.CreateClient("apple-jwks");
            var response = await client.GetStringAsync(AppleJwksUrl, cancellationToken);
            var jwks     = JsonSerializer.Deserialize<JsonElement>(response);

            var keys = new List<SecurityKey>();
            foreach (var key in jwks.GetProperty("keys").EnumerateArray())
            {
                var jwk = new JsonWebKey(key.GetRawText());
                keys.Add(jwk);
            }

            return keys;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao buscar chaves públicas da Apple.");
            return null;
        }
    }
}
