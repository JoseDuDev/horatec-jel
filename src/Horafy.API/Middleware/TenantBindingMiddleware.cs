using System.Security.Claims;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;

namespace Horafy.API.Middleware;

/// <summary>
/// Hardening multi-tenant: para requisições autenticadas de roles que NÃO são
/// PlatformAdmin, garante que o <c>tenant_id</c> do JWT corresponde ao tenant
/// resolvido (por X-Tenant-Slug / subdomínio / domínio). Bloqueia o replay de um
/// token de um tenant contra outro.
///
/// Deve rodar APÓS UseAuthentication (para ter o User) e APÓS o TenantMiddleware
/// (para ter o tenant resolvido), e ANTES de UseAuthorization.
/// </summary>
public sealed class TenantBindingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenantService tenantService)
    {
        var user = context.User;

        // Não autenticado (endpoints anônimos/públicos) → segue.
        if (user.Identity?.IsAuthenticated is not true)
        {
            await next(context);
            return;
        }

        // PlatformAdmin pode operar entre tenants.
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (role == UserRole.PlatformAdmin.ToString())
        {
            await next(context);
            return;
        }

        var jwtTenant = user.FindFirstValue("tenant_id");
        var resolved  = tenantService.TenantId;

        // Só valida quando ambos existem (requisição de fato escopada a um tenant).
        if (!string.IsNullOrEmpty(jwtTenant) && resolved.HasValue &&
            (!Guid.TryParse(jwtTenant, out var jt) || jt != resolved.Value))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "O token não corresponde ao tenant da requisição."
            });
            return;
        }

        await next(context);
    }
}
