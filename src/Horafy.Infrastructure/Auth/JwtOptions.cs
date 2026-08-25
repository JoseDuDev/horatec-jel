namespace Horafy.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = default!;
    public string Issuer { get; set; } = "horafy";
    public string Audience { get; set; } = "horafy-clients";
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// Validade do refresh token para o role Customer — bem mais longa que a de
    /// admin/staff porque o cliente final não deve perceber a sessão expirando.
    /// O access token continua curto (ExpirationMinutes); só o refresh muda.
    /// </summary>
    public int CustomerRefreshTokenExpirationDays { get; set; } = 365;

    /// <summary>Validade do token de integração (M2M), emitido via troca de API key.</summary>
    public int IntegrationTokenExpirationMinutes { get; set; } = 15;
}
