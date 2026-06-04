using FluentAssertions;
using Horafy.Domain.Entities.Tenants;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public class TenantCrudTests
{
    private static Tenant Make() =>
        Tenant.Create("Barbearia do João", "barbearia-joao", TenantVertical.Barbershop, "joao@bar.com");

    // ── UpdateInfo ────────────────────────────────────────────────────
    [Fact]
    public void UpdateInfo_ValidData_UpdatesFields()
    {
        var tenant = Make();
        tenant.UpdateInfo("Nova Barbearia", "novo@email.com", "11999999999",
            "Rua A, 100", "São Paulo", "SP", "01310-100");

        tenant.Name.Should().Be("Nova Barbearia");
        tenant.Email.Should().Be("novo@email.com");
        tenant.Phone.Should().Be("11999999999");
        tenant.City.Should().Be("São Paulo");
        tenant.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateInfo_PreservesTimeZoneIfNotProvided()
    {
        var tenant = Make();
        var originalTz = tenant.TimeZoneId;
        tenant.UpdateInfo("Nome", null, null, null, null, null, null);

        tenant.TimeZoneId.Should().Be(originalTz);
    }

    // ── UpdateTheme ───────────────────────────────────────────────────
    [Fact]
    public void UpdateTheme_ChangesColors()
    {
        var tenant = Make();
        var theme = new TenantTheme
        {
            PrimaryColor = "#FF0000",
            LogoUrl      = "https://cdn.example.com/logo.png"
        };

        tenant.UpdateTheme(theme);

        tenant.Theme.PrimaryColor.Should().Be("#FF0000");
        tenant.Theme.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
    }

    // ── SetCustomDomain / RemoveCustomDomain ──────────────────────────
    [Fact]
    public void SetCustomDomain_NormalizesToLowercase()
    {
        var tenant = Make();
        tenant.SetCustomDomain("MinhaBarbearia.COM.BR");

        tenant.CustomDomain.Should().Be("minhabarbearia.com.br");
    }

    [Fact]
    public void RemoveCustomDomain_SetsNullAndUpdatesTimestamp()
    {
        var tenant = Make();
        tenant.SetCustomDomain("minha.com.br");
        tenant.RemoveCustomDomain();

        tenant.CustomDomain.Should().BeNull();
        tenant.UpdatedAt.Should().NotBeNull();
    }

    // ── Suspend / Activate ────────────────────────────────────────────
    [Fact]
    public void Suspend_SetsStatusSuspended()
    {
        var tenant = Make();
        tenant.Suspend("Inadimplência");

        tenant.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public void Activate_AfterSuspend_SetsStatusActive()
    {
        var tenant = Make();
        tenant.Suspend("Motivo");
        tenant.Activate();

        tenant.Status.Should().Be(TenantStatus.Active);
    }

    // ── SlugNormalization ─────────────────────────────────────────────
    [Fact]
    public void Create_SlugIsLowercaseAndTrimmed()
    {
        var tenant = Tenant.Create("Nome", "  MINHA-BARBEARIA  ", TenantVertical.Barbershop);

        tenant.Slug.Should().Be("minha-barbearia");
    }

    // ── SchemaName ────────────────────────────────────────────────────
    [Fact]
    public void SchemaName_IsCorrectlyFormatted()
    {
        var tenant = Make();

        tenant.SchemaName.Should().Be("tenant_barbearia-joao");
    }
}
