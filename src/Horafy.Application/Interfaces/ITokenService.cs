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

    /// <summary>
    /// Gera um token de serviço (M2M) de curta duração para uma integração de tenant.
    /// Carrega role TenantStaff, tenant_id e source=integration. Usado após a troca
    /// de uma API key válida.
    /// </summary>
    ServiceToken GenerateIntegrationToken(Guid tenantId, string? scopes = null);
}

/// <summary>Par de tokens retornado no login/refresh.</summary>
public sealed record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Token de serviço (M2M) emitido na troca de API key.</summary>
public sealed record ServiceToken(
    string AccessToken,
    DateTimeOffset ExpiresAt);
