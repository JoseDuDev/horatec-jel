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

    private sealed class FakeHttpMessageHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"key":{"id":"abc"}}""")
            });
    }
}
