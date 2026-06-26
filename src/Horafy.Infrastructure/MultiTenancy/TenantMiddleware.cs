using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.MultiTenancy;

/// <summary>
/// Middleware que resolve o tenant antes de qualquer controller ser executado.
///
/// Estratégia de resolução (ordem de prioridade):
///   1. Domínio próprio do cliente    (barbeariadojoao.com.br)
///   2. Subdomínio da plataforma      (joao.horafy.com.br)
///   3. Header X-Tenant-Slug          (para uso interno / mobile)
///
/// Retorna 404 se tenant não for encontrado ou estiver inativo.
/// O resultado é cacheado em memória por 5 minutos para evitar
/// hit no banco a cada requisição.
/// </summary>
public sealed class TenantMiddleware(
    RequestDelegate next,
    ILogger<TenantMiddleware> logger)
{
    private const string PlatformDomain = "horafy.com.br";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantService tenantService,
        ITenantRepository tenantRepository,
        IMemoryCache cache)
    {
        // Endpoints públicos que não precisam de tenant
        if (IsPublicEndpoint(context.Request.Path))
        {
            await next(context);
            return;
        }

        var host = context.Request.Host.Host.ToLowerInvariant();
        var tenantSlug = ResolveTenantSlug(context, host);

        if (string.IsNullOrWhiteSpace(tenantSlug))
        {
            logger.LogWarning("Tenant não identificado para host: {Host}", host);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant não encontrado." });
            return;
        }

        var cacheKey = $"tenant_slug:{tenantSlug}";

        var tenant = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await tenantRepository.GetBySlugAsync(tenantSlug);
        });

        if (tenant is null)
        {
            // Tenta por domínio customizado
            var cacheKeyDomain = $"tenant_domain:{host}";
            tenant = await cache.GetOrCreateAsync(cacheKeyDomain, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return await tenantRepository.GetByCustomDomainAsync(host);
            });
        }

        if (tenant is null || tenant.IsDeleted)
        {
            logger.LogWarning("Tenant não encontrado para slug/domínio: {TenantSlug}", tenantSlug);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Estabelecimento não encontrado." });
            return;
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Conta suspensa. Entre em contato com o suporte." });
            return;
        }

        tenantService.SetTenant(tenant.Id, tenant.SchemaName, tenant.Slug);

        logger.LogDebug(
            "Tenant resolvido: {TenantSlug} | Schema: {Schema}",
            tenant.Slug,
            tenant.SchemaName);

        await next(context);
    }

    /// <summary>
    /// Extrai o slug do tenant a partir do host ou do header X-Tenant-Slug.
    /// </summary>
    private static string? ResolveTenantSlug(HttpContext context, string host)
    {
        // Header explícito (chamadas internas, apps mobile)
        if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var headerSlug))
            return headerSlug.ToString().ToLowerInvariant();

        // Subdomínio: joao.horafy.com.br → "joao"
        if (host.EndsWith($".{PlatformDomain}"))
        {
            var subdomain = host[..^(PlatformDomain.Length + 1)];
            if (!string.IsNullOrWhiteSpace(subdomain) && subdomain != "www" && subdomain != "api")
                return subdomain;
        }

        // Domínio próprio: será resolvido depois por GetByCustomDomainAsync
        // Retorna o host como tentativa de slug para o cache key
        return host == PlatformDomain ? null : host;
    }

    private static bool IsPublicEndpoint(PathString path)
    {
        var publicPrefixes = new[]
        {
            "/health",
            "/metrics",
            "/swagger",
            "/scalar",
            "/api/v1/platform",
            "/api/v1/auth",
            "/api/v1/customers/auth",
            "/api/v1/integrations/token"
        };

        return publicPrefixes.Any(prefix =>
            path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
