namespace Horafy.Application.Interfaces;

/// <summary>Valida um Google ID Token e retorna as informações do usuário.</summary>
public interface IGoogleOAuthService
{
    Task<OAuthUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

/// <summary>Dados do usuário extraídos de um token OAuth.</summary>
public sealed record OAuthUserInfo(
    string ProviderId,
    string Email,
    string? Name,
    string? AvatarUrl);
