using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Tenants.Commands.ActivateTenant;
using Horafy.Application.Features.Tenants.Commands.CreateTenant;
using Horafy.Application.Features.Tenants.Commands.RemoveCustomDomain;
using Horafy.Application.Features.Tenants.Commands.SetCustomDomain;
using Horafy.Application.Features.Tenants.Commands.SuspendTenant;
using Horafy.Application.Features.Tenants.Commands.UpdateTenant;
using Horafy.Application.Features.Tenants.Commands.UpdateTenantTheme;
using Horafy.Application.Features.Tenants.Queries.GetCurrentTenant;
using Horafy.Application.Features.Tenants.Queries.GetTenantBySlug;
using Horafy.Domain.Entities.Tenants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class TenantsController(ISender sender) : ApiControllerBase(sender)
{
    // ── Plataforma (sem tenant context) ──────────────────────────────

    /// <summary>Onboarding: cria um novo estabelecimento e retorna JWT do proprietário.</summary>
    [HttpPost]
    [Route("api/v{version:apiVersion}/platform/tenants")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CreateTenantResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreateTenantCommand(
            request.Name, request.Slug, request.Vertical,
            request.Email, request.Phone, request.City, request.State,
            request.OwnerName, request.OwnerEmail, request.OwnerPassword),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Retorna dados públicos de um tenant pelo slug (landing page).</summary>
    [HttpGet("{slug}")]
    [Route("api/v{version:apiVersion}/platform/tenants/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TenantResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetTenantBySlugQuery(slug), cancellationToken));

    // ── Tenant context (requerem header X-Tenant-Slug ou subdomínio) ─

    /// <summary>Retorna os dados do tenant atual (painel do proprietário).</summary>
    [HttpGet("me")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(typeof(TenantResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetCurrentTenantQuery(), cancellationToken));

    /// <summary>Atualiza dados cadastrais do estabelecimento.</summary>
    [HttpPut("me")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new UpdateTenantCommand(
            request.Name, request.Email, request.Phone,
            request.Address, request.City, request.State, request.ZipCode,
            request.TimeZoneId, request.Locale), cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    /// <summary>Atualiza a identidade visual (cores, logo, redes sociais).</summary>
    [HttpPut("me/theme")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateTheme(
        [FromBody] UpdateTenantThemeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new UpdateTenantThemeCommand(
            request.PrimaryColor, request.SecondaryColor,
            request.BackgroundColor, request.TextColor, request.FontFamily,
            request.LogoUrl, request.FaviconUrl, request.BannerUrl, request.BannerText,
            request.ShowReviews, request.ShowTeam, request.ShowServicePrices,
            request.InstagramUrl, request.WhatsAppNumber, request.FacebookUrl,
            request.SectionsOrder), cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    /// <summary>Vincula um domínio próprio ao tenant (ex: minhaclinica.com.br).</summary>
    [HttpPut("me/domain")]
    [Authorize(Roles = "TenantOwner,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetDomain(
        [FromBody] SetCustomDomainRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetCustomDomainCommand(request.Domain), cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    /// <summary>Remove o domínio próprio do tenant.</summary>
    [HttpDelete("me/domain")]
    [Authorize(Roles = "TenantOwner,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveDomain(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RemoveCustomDomainCommand(), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    // ── Plataforma — ações administrativas ───────────────────────────

    /// <summary>Suspende um tenant (PlatformAdmin only).</summary>
    [HttpPost("api/v{version:apiVersion}/platform/tenants/{id:guid}/suspend")]
    [Authorize(Roles = "PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Suspend(
        Guid id,
        [FromBody] SuspendTenantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SuspendTenantCommand(id, request.Reason), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    /// <summary>Reativa um tenant suspenso (PlatformAdmin only).</summary>
    [HttpPost("api/v{version:apiVersion}/platform/tenants/{id:guid}/activate")]
    [Authorize(Roles = "PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ActivateTenantCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

// ── Request DTOs ─────────────────────────────────────────────────────────────
public sealed record CreateTenantRequest(
    string Name, string Slug, TenantVertical Vertical,
    string? Email, string? Phone, string? City, string? State,
    string OwnerName, string OwnerEmail, string OwnerPassword);

public sealed record UpdateTenantRequest(
    string Name, string? Email, string? Phone,
    string? Address, string? City, string? State, string? ZipCode,
    string? TimeZoneId, string? Locale);

public sealed record UpdateTenantThemeRequest(
    string? PrimaryColor, string? SecondaryColor,
    string? BackgroundColor, string? TextColor, string? FontFamily,
    string? LogoUrl, string? FaviconUrl, string? BannerUrl, string? BannerText,
    bool ShowReviews = true, bool ShowTeam = true, bool ShowServicePrices = true,
    string? InstagramUrl = null, string? WhatsAppNumber = null, string? FacebookUrl = null,
    string? SectionsOrder = null);

public sealed record SetCustomDomainRequest(string Domain);
public sealed record SuspendTenantRequest(string Reason);
