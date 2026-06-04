using Horafy.Shared;

namespace Horafy.Application.Features.Auth;

/// <summary>Erros de domínio relacionados à autenticação.</summary>
public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new(
        "Auth.InvalidCredentials",
        "E-mail ou senha incorretos.",
        ErrorType.Unauthorized);

    public static readonly Error InvalidOAuthToken = new(
        "Auth.InvalidOAuthToken",
        "Token OAuth inválido ou expirado.",
        ErrorType.Unauthorized);

    public static readonly Error InvalidRefreshToken = new(
        "Auth.InvalidRefreshToken",
        "Refresh token inválido ou expirado.",
        ErrorType.Unauthorized);

    public static readonly Error EmailAlreadyRegistered = new(
        "Auth.EmailAlreadyRegistered",
        "Este e-mail já está cadastrado.",
        ErrorType.Conflict);

    public static readonly Error TenantNotFound = new(
        "Auth.TenantNotFound",
        "Estabelecimento não encontrado.",
        ErrorType.NotFound);

    public static readonly Error UserNotFound = new(
        "Auth.UserNotFound",
        "Usuário não encontrado.",
        ErrorType.NotFound);
}
