namespace Horafy.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = default!;
    public string Issuer { get; set; } = "horafy";
    public string Audience { get; set; } = "horafy-clients";
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>Validade do token de integração (M2M), emitido via troca de API key.</summary>
    public int IntegrationTokenExpirationMinutes { get; set; } = 15;
}
