using FluentAssertions;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Horafy.Infrastructure.Tests.MultiTenancy;

public sealed class TenantMiddlewareTests
{
    private readonly Mock<ITenantRepository> _tenantRepoMock = new();
    private readonly Mock<ICurrentTenantService> _tenantServiceMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly TenantMiddleware _middleware;

    public TenantMiddlewareTests()
    {
        _middleware = CriarMiddleware("agenda.mjml.com.br", "alugue.mjml.com.br");
    }

    private static TenantMiddleware CriarMiddleware(params string[] platformDomains)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(platformDomains
                .Select((d, i) => new KeyValuePair<string, string?>($"Platform:Domains:{i}", d)))
            .Build();

        return new TenantMiddleware(
            _ => Task.CompletedTask,
            NullLogger<TenantMiddleware>.Instance,
            config);
    }

    [Fact]
    public async Task Invoke_ComSubdominioPlatforma_DeveResolverTenant()
    {
        // Arrange
        var tenant = Tenant.Create("Barbearia João", "joao", TenantVertical.Barbershop);

        _tenantRepoMock
            .Setup(r => r.GetBySlugAsync("joao", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var context = CriarHttpContext("joao.agenda.mjml.com.br");

        // Act
        await _middleware.InvokeAsync(context, _tenantServiceMock.Object, _tenantRepoMock.Object, _cache);

        // Assert
        _tenantServiceMock.Verify(
            s => s.SetTenant(tenant.Id, tenant.SchemaName, tenant.Slug),
            Times.Once);
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("joao.agenda.mjml.com.br")]
    [InlineData("joao.alugue.mjml.com.br")]
    public async Task Invoke_ComSubdominioDeMarca_DeveResolverTenant(string host)
    {
        // A separação de marcas publica o mesmo backend sob Agenda e Alugue;
        // um subdomínio de qualquer marca configurada precisa achar o tenant.
        var tenant = Tenant.Create("Barbearia João", "joao", TenantVertical.Barbershop);

        _tenantRepoMock
            .Setup(r => r.GetBySlugAsync("joao", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var context = CriarHttpContext(host);

        await _middleware.InvokeAsync(context, _tenantServiceMock.Object, _tenantRepoMock.Object, _cache);

        _tenantServiceMock.Verify(
            s => s.SetTenant(tenant.Id, tenant.SchemaName, tenant.Slug),
            Times.Once);
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Invoke_ComDominioNaoConfigurado_NaoDeveTratarComoSubdominioDaPlataforma()
    {
        // marcanova.com.br não está na config: o host inteiro vira tentativa de
        // domínio próprio, não "joao".
        var middleware = CriarMiddleware("agenda.mjml.com.br");

        _tenantRepoMock
            .Setup(r => r.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        _tenantRepoMock
            .Setup(r => r.GetByCustomDomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var context = CriarHttpContext("joao.marcanova.com.br");

        await middleware.InvokeAsync(context, _tenantServiceMock.Object, _tenantRepoMock.Object, _cache);

        _tenantRepoMock.Verify(
            r => r.GetBySlugAsync("joao", It.IsAny<CancellationToken>()),
            Times.Never);
        _tenantRepoMock.Verify(
            r => r.GetByCustomDomainAsync("joao.marcanova.com.br", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Invoke_ComPlatformDomainSingular_DeveContinuarFuncionando()
    {
        // Compatibilidade: instalações que só têm `Platform:Domain` (string).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Platform:Domain", "horafy.com.br")])
            .Build();

        var middleware = new TenantMiddleware(
            _ => Task.CompletedTask,
            NullLogger<TenantMiddleware>.Instance,
            config);

        var tenant = Tenant.Create("Barbearia João", "joao", TenantVertical.Barbershop);
        _tenantRepoMock
            .Setup(r => r.GetBySlugAsync("joao", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var context = CriarHttpContext("joao.horafy.com.br");

        await middleware.InvokeAsync(context, _tenantServiceMock.Object, _tenantRepoMock.Object, _cache);

        _tenantServiceMock.Verify(
            s => s.SetTenant(tenant.Id, tenant.SchemaName, tenant.Slug),
            Times.Once);
    }

    [Fact]
    public async Task Invoke_ComHeaderXTenantSlug_DeveResolverTenant()
    {
        // Arrange
        var tenant = Tenant.Create("Clínica Med", "clinica-med", TenantVertical.MedicalClinic);

        _tenantRepoMock
            .Setup(r => r.GetBySlugAsync("clinica-med", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var context = CriarHttpContext("api.mjml.com.br");
        context.Request.Headers["X-Tenant-Slug"] = "clinica-med";

        // Act
        await _middleware.InvokeAsync(context, _tenantServiceMock.Object, _tenantRepoMock.Object, _cache);

        // Assert
        _tenantServiceMock.Verify(
            s => s.SetTenant(tenant.Id, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Invoke_TenantNaoEncontrado_DeveRetornar404()
    {
        // Arrange
        _tenantRepoMock
            .Setup(r => r.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        _tenantRepoMock
            .Setup(r => r.GetByCustomDomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var context = CriarHttpContext("naoexiste.agenda.mjml.com.br");

        // Act
        await _middleware.InvokeAsync(context, _tenantServiceMock.Object, _tenantRepoMock.Object, _cache);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        _tenantServiceMock.Verify(
            s => s.SetTenant(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Invoke_TenantSuspenso_DeveRetornar403()
    {
        // Arrange
        var tenant = Tenant.Create("Suspenso", "suspenso", TenantVertical.Other);
        tenant.Suspend("teste");

        _tenantRepoMock
            .Setup(r => r.GetBySlugAsync("suspenso", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var context = CriarHttpContext("suspenso.agenda.mjml.com.br");

        // Act
        await _middleware.InvokeAsync(context, _tenantServiceMock.Object, _tenantRepoMock.Object, _cache);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/swagger/index.html")]
    [InlineData("/scalar")]
    [InlineData("/api/v1/platform/tenants")]
    public async Task Invoke_EndpointPublico_DevePassarSemVerificarTenant(string path)
    {
        // Arrange
        var context = CriarHttpContext("mjml.com.br", path);

        // Act
        await _middleware.InvokeAsync(context, _tenantServiceMock.Object, _tenantRepoMock.Object, _cache);

        // Assert
        _tenantRepoMock.Verify(
            r => r.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static HttpContext CriarHttpContext(string host, string path = "/api/v1/test")
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        context.Response.Body = new System.IO.MemoryStream();
        return context;
    }
}
