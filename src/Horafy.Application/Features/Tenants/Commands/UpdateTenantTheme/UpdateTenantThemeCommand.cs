using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdateTenantTheme;

public sealed record UpdateTenantThemeCommand(
    string? PrimaryColor,
    string? SecondaryColor,
    string? BackgroundColor,
    string? TextColor,
    string? FontFamily,
    string? LogoUrl,
    string? FaviconUrl,
    string? BannerUrl,
    string? BannerText,
    bool ShowReviews,
    bool ShowTeam,
    bool ShowServicePrices,
    string? InstagramUrl,
    string? WhatsAppNumber,
    string? FacebookUrl,
    string? SectionsOrder) : IRequest<Result>;

internal sealed class UpdateTenantThemeCommandHandler(
    ICurrentTenantService currentTenant,
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateTenantThemeCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTenantThemeCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(
            currentTenant.TenantId.Value, cancellationToken);

        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        var theme = new TenantTheme
        {
            PrimaryColor      = request.PrimaryColor    ?? tenant.Theme.PrimaryColor,
            SecondaryColor    = request.SecondaryColor  ?? tenant.Theme.SecondaryColor,
            BackgroundColor   = request.BackgroundColor ?? tenant.Theme.BackgroundColor,
            TextColor         = request.TextColor       ?? tenant.Theme.TextColor,
            FontFamily        = request.FontFamily      ?? tenant.Theme.FontFamily,
            LogoUrl           = request.LogoUrl         ?? tenant.Theme.LogoUrl,
            FaviconUrl        = request.FaviconUrl      ?? tenant.Theme.FaviconUrl,
            BannerUrl         = request.BannerUrl       ?? tenant.Theme.BannerUrl,
            BannerText        = request.BannerText      ?? tenant.Theme.BannerText,
            ShowReviews       = request.ShowReviews,
            ShowTeam          = request.ShowTeam,
            ShowServicePrices = request.ShowServicePrices,
            InstagramUrl      = request.InstagramUrl    ?? tenant.Theme.InstagramUrl,
            WhatsAppNumber    = request.WhatsAppNumber  ?? tenant.Theme.WhatsAppNumber,
            FacebookUrl       = request.FacebookUrl     ?? tenant.Theme.FacebookUrl,
            SectionsOrder     = request.SectionsOrder   ?? tenant.Theme.SectionsOrder
        };

        tenant.UpdateTheme(theme);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
