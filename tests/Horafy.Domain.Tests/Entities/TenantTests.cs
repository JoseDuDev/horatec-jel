using FluentAssertions;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Events.Tenants;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public sealed class TenantTests
{
    [Fact]
    public void Create_ComDadosValidos_DeveCriarTenantComSucesso()
    {
        // Arrange
        const string nome = "Barbearia do João";
        const string slug = "barbearia-joao";
        const TenantVertical vertical = TenantVertical.Barbershop;

        // Act
        var tenant = Tenant.Create(nome, slug, vertical);

        // Assert
        tenant.Name.Should().Be(nome);
        tenant.Slug.Should().Be(slug);
        tenant.Vertical.Should().Be(vertical);
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.Plan.Should().Be(TenantPlan.Free);
        tenant.TrialEndsAt.Should().NotBeNull();
        tenant.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_DeveGerarIdUnico()
    {
        var t1 = Tenant.Create("A", "slug-a", TenantVertical.Barbershop);
        var t2 = Tenant.Create("B", "slug-b", TenantVertical.Barbershop);

        t1.Id.Should().NotBe(t2.Id);
    }

    [Fact]
    public void Create_DeveSlugarEmMinusculas()
    {
        var tenant = Tenant.Create("Salão Festa", "SALAO-FESTA", TenantVertical.EventHall);

        tenant.Slug.Should().Be("salao-festa");
    }

    [Fact]
    public void Create_DeveDispararTenantCreatedEvent()
    {
        var tenant = Tenant.Create("Test", "test-slug", TenantVertical.Other);

        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantCreatedEvent>();

        var evt = (TenantCreatedEvent)tenant.DomainEvents.First();
        evt.TenantId.Should().Be(tenant.Id);
        evt.Slug.Should().Be("test-slug");
    }

    [Fact]
    public void SchemaName_DeveRetornarFormatoCorreto()
    {
        var tenant = Tenant.Create("X", "minha-clinica", TenantVertical.MedicalClinic);

        tenant.SchemaName.Should().Be("tenant_minha-clinica");
    }

    [Fact]
    public void Delete_DeveMarcarComoExcluido()
    {
        var tenant = Tenant.Create("Y", "slug-y", TenantVertical.Other);

        tenant.Delete("admin");

        tenant.IsDeleted.Should().BeTrue();
        tenant.DeletedAt.Should().NotBeNull();
        tenant.DeletedBy.Should().Be("admin");
    }

    [Fact]
    public void Restore_DeveDesfazerExclusao()
    {
        var tenant = Tenant.Create("Z", "slug-z", TenantVertical.Other);
        tenant.Delete("admin");

        tenant.Restore();

        tenant.IsDeleted.Should().BeFalse();
        tenant.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Suspend_DeveAlterarStatusParaSuspended()
    {
        var tenant = Tenant.Create("W", "slug-w", TenantVertical.Other);

        tenant.Suspend("Inadimplência");

        tenant.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public void UpgradePlan_DeveAtualizarPlanoEDataRenovacao()
    {
        var tenant = Tenant.Create("V", "slug-v", TenantVertical.Other);
        var renewsAt = DateTimeOffset.UtcNow.AddMonths(1);

        tenant.UpgradePlan(TenantPlan.Professional, renewsAt);

        tenant.Plan.Should().Be(TenantPlan.Professional);
        tenant.PlanRenewsAt.Should().Be(renewsAt);
    }

    [Fact]
    public void CompleteOnboarding_DeveDefinirOnboardingCompletedAt()
    {
        var tenant = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        tenant.ClearDomainEvents();

        tenant.CompleteOnboarding();

        tenant.OnboardingCompletedAt.Should().NotBeNull();
        tenant.IsOnboardingCompleted.Should().BeTrue();
        tenant.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsOnboardingCompleted_DeveRetornarFalseParaNovoTenant()
    {
        var tenant = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);

        tenant.IsOnboardingCompleted.Should().BeFalse();
    }
}
