using Google.Apis.Auth;
using Horafy.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.Auth;

internal sealed class GoogleOAuthService(
    IConfiguration configuration,
    ILogger<GoogleOAuthService> logger) : IGoogleOAuthService
{
    private readonly string? _clientId = configuration["Google:ClientId"];

    public async Task<OAuthUserInfo?> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings();

            if (!string.IsNullOrWhiteSpace(_clientId))
                settings.Audience = [_clientId];

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new OAuthUserInfo(
                ProviderId: payload.Subject,
                Email:      payload.Email,
                Name:       payload.Name,
                AvatarUrl:  payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Google ID token inválido.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao validar Google ID token.");
            return null;
        }
    }
}
