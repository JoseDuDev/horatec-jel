using System.Net.Http.Json;
using Horafy.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Horafy.Infrastructure.Gateways;

internal sealed class EvolutionApiWhatsAppService(
    HttpClient httpClient,
    IOptions<EvolutionApiOptions> options,
    ILogger<EvolutionApiWhatsAppService> logger) : IWhatsAppService
{
    private readonly EvolutionApiOptions _opts = options.Value;

    /// <summary>
    /// Mesma postura do <c>SmtpEmailService</c>: sem configuração, loga e segue.
    /// Sem esta guarda a env vazia deixa o HttpClient sem BaseAddress (ver
    /// DependencyInjection), o POST relativo estoura InvalidOperationException e o
    /// <c>NotificationLogger</c> repropaga — derrubando o consumer inteiro, o que
    /// impede até o e-mail que vem depois do WhatsApp de ser enviado.
    /// </summary>
    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opts.BaseUrl) && !string.IsNullOrWhiteSpace(_opts.InstanceName);

    public async Task SendTextAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning(
                "Evolution API não configurada — WhatsApp NÃO enviado. Para: {Phone}\n{Message}",
                phoneNumber, message);
            return;
        }

        var body = new { number = phoneNumber, text = message };
        logger.LogInformation("Enviando WhatsApp para {Phone}", phoneNumber);
        var response = await httpClient.PostAsJsonAsync(
            $"/message/sendText/{_opts.InstanceName}", body, ct);
        response.EnsureSuccessStatusCode();
    }
}
