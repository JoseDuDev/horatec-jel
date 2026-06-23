using FluentAssertions;
using Horafy.Domain.Entities.Tenants;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public sealed class PlanLimitsTests
{
    [Theory]
    [InlineData(TenantPlan.Free, 5, 2, 5)]
    [InlineData(TenantPlan.Starter, 10, 2, 10)]
    [InlineData(TenantPlan.Professional, 20, 3, 20)]
    public void For_RetornaLimitesDoPlano(TenantPlan plan, int svc, int res, int items)
    {
        var limits = PlanLimits.For(plan);
        limits.MaxServices.Should().Be(svc);
        limits.MaxResources.Should().Be(res);
        limits.MaxRentableItems.Should().Be(items);
    }

    [Fact]
    public void For_Enterprise_EhIlimitado()
    {
        var limits = PlanLimits.For(TenantPlan.Enterprise);
        limits.MaxServices.Should().Be(PlanLimits.Unlimited);
        limits.ServicesReached(1_000_000).Should().BeFalse();
        limits.ResourcesReached(1_000_000).Should().BeFalse();
        limits.RentableItemsReached(1_000_000).Should().BeFalse();
    }

    [Theory]
    [InlineData(4, false)]  // abaixo do limite (Free = 5 serviços)
    [InlineData(5, true)]   // no limite → atingido
    [InlineData(6, true)]   // acima
    public void ServicesReached_RespeitaLimite(int current, bool reached)
    {
        PlanLimits.For(TenantPlan.Free).ServicesReached(current).Should().Be(reached);
    }
}

public sealed class TenantCapabilityTests
{
    [Fact]
    public void Create_PorPadrao_TemAmbasCapacidades()
    {
        var tenant = Tenant.Create("X", "x", TenantVertical.Other);

        tenant.Has(TenantCapability.Appointments).Should().BeTrue();
        tenant.Has(TenantCapability.Rentals).Should().BeTrue();
    }

    [Fact]
    public void Create_ComCapacidadeEspecifica_RestringeModulos()
    {
        var tenant = Tenant.Create("X", "x", TenantVertical.ToolRental,
            capabilities: TenantCapability.Rentals);

        tenant.Has(TenantCapability.Rentals).Should().BeTrue();
        tenant.Has(TenantCapability.Appointments).Should().BeFalse();
    }

    [Fact]
    public void SetCapabilities_AtualizaModulosEUpdatedAt()
    {
        var tenant = Tenant.Create("X", "x", TenantVertical.Other);

        tenant.SetCapabilities(TenantCapability.Appointments);

        tenant.Has(TenantCapability.Appointments).Should().BeTrue();
        tenant.Has(TenantCapability.Rentals).Should().BeFalse();
        tenant.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Limits_RefleteOPlano()
    {
        var tenant = Tenant.Create("X", "x", TenantVertical.Other, plan: TenantPlan.Professional);

        tenant.Limits.MaxServices.Should().Be(20);
        tenant.Limits.MaxResources.Should().Be(3);
    }
}
