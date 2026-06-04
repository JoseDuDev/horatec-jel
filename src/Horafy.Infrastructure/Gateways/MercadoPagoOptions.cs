namespace Horafy.Infrastructure.Gateways;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";
    public string AccessToken    { get; set; } = string.Empty;
    public string WebhookSecret  { get; set; } = string.Empty;
    public string NotificationUrl { get; set; } = string.Empty;
}
