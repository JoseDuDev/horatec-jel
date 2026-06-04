using Horafy.Domain.Entities.Users;
using System.Security.Claims;

namespace Horafy.Application.Interfaces;

/// <summary>
/// Contrato para geração e validação de JWT (access + refresh token).
/// </summary>
public interface ITokenService
{
    /// <summary>Gera access token + refresh token para o usuário.</summary>
    TokenPair GenerateTokens(User user);

    /// <summary>
    /// Valida um refresh token e retorna as claims se válido.
    /// Retorna null se o token for inválido ou expirado.
    /// </summary>
    ClaimsPrincipal? ValidateRefreshToken(string refreshToken);
}

/// <summary>Par de tokens retornado no login/refresh.</summary>
public sealed record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);
