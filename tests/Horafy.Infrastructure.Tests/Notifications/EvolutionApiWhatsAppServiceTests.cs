using System.Net;
using FluentAssertions;
using Horafy.Infrastructure.Gateways;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Horafy.Infrastructure.Tests.Notifications;

public sealed class EvolutionApiWhatsAppServiceTests
{
    private static EvolutionApiWhatsAppService MakeService(HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(status);
        var client  = new HttpClient(handler) { BaseAddress = new Uri("http://evo.local") };
        var opts    = Options.Create(new EvolutionApiOptions
        {
            BaseUrl = "http://evo.local", ApiKey = "key", InstanceName = "horafy"
        });
        return new EvolutionApiWhatsAppService(client, opts,
            NullLogger<EvolutionApiWhatsAppService>.Instance);
    }

    [Fact]
    public async Task SendTextAsync_SuccessResponse_DoesNotThrow()
    {
        var svc = MakeService(HttpStatusCode.OK);
        var act = () => svc.SendTextAsync("5511999999999", "Olá!", default);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendTextAsync_ErrorResponse_ThrowsHttpRequestException()
    {
        var svc = MakeService(HttpStatusCode.InternalServerError);
        var act = () => svc.SendTextAsync("5511999999999", "Olá!", default);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // Produção roda assim hoje: nenhuma env EvolutionApi__* no compose do Coolify.
    // Sem a guarda de IsConfigured, o HttpClient fica sem BaseAddress e o POST
    // relativo estoura InvalidOperationException — o NotificationLogger repropaga
    // e derruba o consumer, levando junto o e-mail que seria enviado depois.
    [Fact]
    public async Task SendTextAsync_NotConfigured_DoesNotThrowAndDoesNotCallApi()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var client  = new HttpClient(handler); // sem BaseAddress, como na DI com BaseUrl vazio
        var opts    = Options.Create(new EvolutionApiOptions()); // tudo vazio
        var svc     = new EvolutionApiWhatsAppService(client, opts,
            NullLogger<EvolutionApiWhatsAppService>.Instance);

        var act = () => svc.SendTextAsync("5511999999999", "Olá!", default);

        await act.Should().NotThrowAsync();
        handler.Calls.Should().Be(0);
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"key":{"id":"abc"}}""")
            });
        }
    }
}
