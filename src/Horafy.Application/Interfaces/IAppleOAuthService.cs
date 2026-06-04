namespace Horafy.Application.Interfaces;

/// <summary>Valida um Apple Identity Token e retorna as informações do usuário.</summary>
public interface IAppleOAuthService
{
    Task<OAuthUserInfo?> ValidateAsync(string identityToken, CancellationToken cancellationToken = default);
}
