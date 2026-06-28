using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Queries.GetCurrentTenant;

public sealed record GetCurrentTenantQuery : IRequest<Result<TenantResult>>;

public sealed record TenantResult(
    Guid   Id,
    string Name,
    string Slug,
    string? CustomDomain,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string  TimeZoneId,
    string  Locale,
    TenantStatus Status,
    TenantPlan   Plan,
    TenantCapability Capabilities,
    TenantVertical Vertical,
    TenantThemeResult Theme,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? PlanRenewsAt,
    bool                     IsOnboardingCompleted,
    CancellationPolicyResult CancellationPolicy,
    LoyaltySettingsResult    LoyaltySettings,
    ReminderSettingsResult   ReminderSettings);

public sealed record CancellationPolicyResult(
    int     MinCancellationHours,
    decimal CancellationFeePercent,
    bool    AllowCustomerCancellation);

public sealed record LoyaltySettingsResult(
    bool    IsEnabled,
    decimal CreditRatePercent,
    decimal MinBookingAmount);

public sealed record ReminderSettingsResult(
    bool Enabled,
    int  FirstReminderHours,
    int  SecondReminderHours);

public sealed record TenantThemeResult(
    string PrimaryColor,
    string SecondaryColor,
    string BackgroundColor,
    string TextColor,
    string FontFamily,
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
    string SectionsOrder);

internal sealed class GetCurrentTenantQueryHandler(
    ICurrentTenantService currentTenant,
    ITenantRepository tenantRepository) : IRequestHandler<GetCurrentTenantQuery, Result<TenantResult>>
{
    public async Task<Result<TenantResult>> Handle(
        GetCurrentTenantQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure<TenantResult>(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(
            currentTenant.TenantId.Value, cancellationToken);

        if (tenant is null) return Result.Failure<TenantResult>(TenantErrors.NotFound);

        return Result.Success(ToResult(tenant));
    }

    internal static TenantResult ToResult(Domain.Entities.Tenants.Tenant t) => new(
        t.Id, t.Name, t.Slug, t.CustomDomain,
        t.Email, t.Phone, t.Address, t.City, t.State, t.ZipCode,
        t.TimeZoneId, t.Locale, t.Status, t.Plan, t.Capabilities, t.Vertical,
        new TenantThemeResult(
            t.Theme.PrimaryColor, t.Theme.SecondaryColor,
            t.Theme.BackgroundColor, t.Theme.TextColor, t.Theme.FontFamily,
            t.Theme.LogoUrl, t.Theme.FaviconUrl,
            t.Theme.BannerUrl, t.Theme.BannerText,
            t.Theme.ShowReviews, t.Theme.ShowTeam, t.Theme.ShowServicePrices,
            t.Theme.InstagramUrl, t.Theme.WhatsAppNumber, t.Theme.FacebookUrl,
            t.Theme.SectionsOrder),
        t.TrialEndsAt, t.PlanRenewsAt,
        t.IsOnboardingCompleted,
        new CancellationPolicyResult(
            t.CancellationPolicy.MinCancellationHours,
            t.CancellationPolicy.CancellationFeePercent,
            t.CancellationPolicy.AllowCustomerCancellation),
        new LoyaltySettingsResult(
            t.LoyaltySettings.IsEnabled,
            t.LoyaltySettings.CreditRatePercent,
            t.LoyaltySettings.MinBookingAmount),
        new ReminderSettingsResult(
            t.ReminderSettings.Enabled,
            t.ReminderSettings.FirstReminderHours,
            t.ReminderSettings.SecondReminderHours));
}
