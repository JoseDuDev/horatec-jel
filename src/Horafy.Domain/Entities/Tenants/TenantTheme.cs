namespace Horafy.Domain.Entities.Tenants;

/// <summary>
/// Value Object representando a identidade visual do tenant.
/// Armazenado como JSON (owned entity) na tabela do tenant.
/// </summary>
public sealed class TenantTheme
{
    public string PrimaryColor { get; set; } = "#2563EB";
    public string SecondaryColor { get; set; } = "#7C3AED";
    public string BackgroundColor { get; set; } = "#F8FAFC";
    public string TextColor { get; set; } = "#1E293B";
    public string FontFamily { get; set; } = "Inter";

    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? BannerText { get; set; }

    public bool ShowReviews { get; set; } = true;
    public bool ShowTeam { get; set; } = true;
    public bool ShowServicePrices { get; set; } = true;

    public string? InstagramUrl { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? FacebookUrl { get; set; }

    /// <summary>
    /// Ordem das seções da landing page, separadas por vírgula.
    /// </summary>
    public string SectionsOrder { get; set; } = "banner,services,team,reviews,contact";
}
