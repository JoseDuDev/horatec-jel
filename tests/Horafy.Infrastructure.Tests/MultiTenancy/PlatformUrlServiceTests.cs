using FluentAssertions;
using Horafy.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Horafy.Infrastructure.Tests.MultiTenancy;

public sealed class PlatformUrlServiceTests
{
    private static PlatformUrlService Criar(string? origin)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Platform:Domains:0"] = "agenda.mjml.com.br",
                ["Platform:Domains:1"] = "alugue.mjml.com.br",
                ["Platform:Domain"]    = "agenda.mjml.com.br",
            })
            .Build();

        var context = new DefaultHttpContext();
        if (origin is not null) context.Request.Headers.Origin = origin;

        return new PlatformUrlService(
            new HttpContextAccessor { HttpContext = context },
            config);
    }

    [Theory]
    [InlineData("https://alugue.mjml.com.br",      "https://alugue.mjml.com.br/reset-password?token=abc")]
    [InlineData("https://agenda.mjml.com.br",      "https://agenda.mjml.com.br/reset-password?token=abc")]
    [InlineData("https://joao.alugue.mjml.com.br", "https://joao.alugue.mjml.com.br/reset-password?token=abc")]
    public void BuildUrl_ComOrigemDaPlataforma_UsaOHostDaMarca(string origin, string esperado)
    {
        Criar(origin).BuildUrl("/reset-password?token=abc").Should().Be(esperado);
    }

    // Sem browser (job em background, chamada de integração) não há Origin: cai para
    // o domínio de configuração em vez de gerar um link quebrado.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildUrl_SemOrigem_CaiParaPlatformDomain(string? origin)
    {
        Criar(origin).BuildUrl("/reset-password?token=abc")
            .Should().Be("https://agenda.mjml.com.br/reset-password?token=abc");
    }

    // O ponto sensível: o link de redefinição carrega um token válido na query.
    // Uma origem forjada não pode entrar no e-mail que vai para o usuário real.
    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("https://agenda.mjml.com.br.evil.com")]
    [InlineData("https://mjml.com.br")]                 // domínio raiz não é marca
    [InlineData("http://alugue.mjml.com.br")]           // http puro
    [InlineData("nao-e-uma-url")]
    public void BuildUrl_ComOrigemForaDaPlataforma_IgnoraEUsaPlatformDomain(string origin)
    {
        Criar(origin).BuildUrl("/reset-password?token=abc")
            .Should().Be("https://agenda.mjml.com.br/reset-password?token=abc");
    }

    [Fact]
    public void BuildUrl_SemBarraInicial_NaoDuplicaASeparacao()
    {
        Criar("https://alugue.mjml.com.br").BuildUrl("reset-password")
            .Should().Be("https://alugue.mjml.com.br/reset-password");
    }
}
