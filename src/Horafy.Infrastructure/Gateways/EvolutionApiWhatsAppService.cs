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

    public async Task SendTextAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        var body = new { number = phoneNumber, text = message };
        logger.LogInformation("Enviando WhatsApp para {Phone}", phoneNumber);
        var response = await httpClient.PostAsJsonAsync(
            $"/message/sendText/{_opts.InstanceName}", body, ct);
        response.EnsureSuccessStatusCode();
    }
}
