using Horafy.Domain.Entities.Base;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Events.Tenants;

namespace Horafy.Domain.Entities.Tenants;

/// <summary>
/// Agregado raiz que representa um tenant (cliente da plataforma).
/// Cada tenant possui schema próprio no PostgreSQL e identidade visual independente.
/// </summary>
public sealed class Tenant : BaseEntity
{
    private Tenant() { } // EF Core

    public string Name { get; private set; } = default!;

    /// <summary>
    /// Identificador único usado em URLs: subdomain.horafy.com.br ou horafy.com.br/{slug}
    /// </summary>
    public string Slug { get; private set; } = default!;

    /// <summary>
    /// Domínio próprio do cliente: minhaclinica.com.br (CNAME apontando para a plataforma)
    /// </summary>
    public string? CustomDomain { get; private set; }

    /// <summary>
    /// Nome do schema no PostgreSQL: tenant_{slug}
    /// </summary>
    public string SchemaName => $"tenant_{Slug}";

    public TenantStatus Status { get; private set; } = TenantStatus.Active;
    public TenantPlan Plan { get; private set; } = TenantPlan.Free;
    public TenantVertical Vertical { get; private set; }

    /// <summary>
    /// Módulos contratados (agendamento e/ou locação). Default: ambos, para preservar
    /// o comportamento de tenants pré-existentes. A plataforma restringe ao vender um pacote.
    /// </summary>
    public TenantCapability Capabilities { get; private set; } =
        TenantCapability.Appointments | TenantCapability.Rentals;

    // Configurações de contato
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? ZipCode { get; private set; }

    // Identidade visual (serializada como JSON na infra)
    public TenantTheme Theme { get; private set; } = new();

    // Configurações operacionais
    public string TimeZoneId { get; private set; } = "America/Sao_Paulo";
    public string Locale { get; private set; } = "pt-BR";

    public DateTimeOffset? TrialEndsAt { get; private set; }
    public DateTimeOffset? PlanRenewsAt { get; private set; }
    public CancellationPolicy CancellationPolicy { get; private set; } = CancellationPolicy.Default;
    public PaymentSettings PaymentSettings { get; private set; } = PaymentSettings.Default;
    public LoyaltySettings LoyaltySettings { get; private set; } = LoyaltySettings.Default;
    public ReminderSettings ReminderSettings { get; private set; } = ReminderSettings.Default;
    public DateTimeOffset? OnboardingCompletedAt { get; private set; }
    public bool IsOnboardingCompleted => OnboardingCompletedAt.HasValue;

    public static Tenant Create(
        string name,
        string slug,
        TenantVertical vertical,
        string? email = null,
        TenantCapability capabilities = TenantCapability.Appointments | TenantCapability.Rentals,
        TenantPlan plan = TenantPlan.Free)
    {
        var tenant = new Tenant
        {
            Name = name,
            Slug = slug.ToLowerInvariant().Trim(),
            Vertical = vertical,
            Email = email,
            Capabilities = capabilities,
            Plan = plan,
            TrialEndsAt = DateTimeOffset.UtcNow.AddDays(14)
        };

        tenant.RaiseDomainEvent(new TenantCreatedEvent(tenant.Id, tenant.Name, tenant.Slug));

        return tenant;
    }

    public void UpdateTheme(TenantTheme theme)
    {
        Theme = theme;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Atualiza dados cadastrais do estabelecimento.</summary>
    public void UpdateInfo(
        string name,
        string? email,
        string? phone,
        string? address,
        string? city,
        string? state,
        string? zipCode,
        string? timeZoneId = null,
        string? locale = null)
    {
        Name      = name.Trim();
        Email     = email?.Trim();
        Phone     = phone?.Trim();
        Address   = address?.Trim();
        City      = city?.Trim();
        State     = state?.Trim();
        ZipCode   = zipCode?.Trim();

        if (!string.IsNullOrWhiteSpace(timeZoneId)) TimeZoneId = timeZoneId;
        if (!string.IsNullOrWhiteSpace(locale))     Locale     = locale;

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetCustomDomain(string domain)
    {
        CustomDomain = domain.ToLowerInvariant().Trim();
        UpdatedAt    = DateTimeOffset.UtcNow;
    }

    public void RemoveCustomDomain()
    {
        CustomDomain = null;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        Status = TenantStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Suspend(string reason)
    {
        Status = TenantStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpgradePlan(TenantPlan plan, DateTimeOffset renewsAt)
    {
        Plan = plan;
        PlanRenewsAt = renewsAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Indica se o tenant tem a capacidade (módulo) informada.</summary>
    public bool Has(TenantCapability capability) => Capabilities.HasFlag(capability);

    /// <summary>Define os módulos contratados (ação da plataforma).</summary>
    public void SetCapabilities(TenantCapability capabilities)
    {
        Capabilities = capabilities;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Define o plano (ação da plataforma), sem alterar a data de renovação.</summary>
    public void SetPlan(TenantPlan plan)
    {
        Plan = plan;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Limites de cadastro do plano atual.</summary>
    public PlanLimits Limits => PlanLimits.For(Plan);

    public void UpdateCancellationPolicy(int minHours, decimal feePercent, bool allowCustomer)
    {
        CancellationPolicy = CancellationPolicy.Create(minHours, feePercent, allowCustomer);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdatePaymentSettings(bool requiresPayment, DepositMode mode, decimal value)
    {
        PaymentSettings = PaymentSettings.Create(requiresPayment, mode, value);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateLoyaltySettings(bool isEnabled, decimal ratePercent, decimal minAmount)
    {
        LoyaltySettings = LoyaltySettings.Create(isEnabled, ratePercent, minAmount);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateReminderSettings(bool enabled, int firstReminderHours, int secondReminderHours)
    {
        ReminderSettings = ReminderSettings.Create(enabled, firstReminderHours, secondReminderHours);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void CompleteOnboarding()
    {
        OnboardingCompletedAt = DateTimeOffset.UtcNow;
        UpdatedAt             = DateTimeOffset.UtcNow;
    }
}

public enum TenantStatus
{
    Active,
    Suspended,
    Trial,
    Cancelled
}

public enum TenantPlan
{
    Free,
    Starter,
    Professional,
    Enterprise
}

public enum TenantVertical
{
    Barbershop,
    EventHall,
    SportsCourt,
    ToyRental,
    ToolRental,
    MedicalClinic,
    AestheticClinic,
    Other
}
