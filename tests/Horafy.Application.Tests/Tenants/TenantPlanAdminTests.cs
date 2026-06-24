using System.Linq.Expressions;
using FluentAssertions;
using Horafy.Application.Features.Tenants.Commands.UpdateTenantPlan;
using Horafy.Application.Features.Tenants.Queries.GetTenantUsage;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public class TenantPlanAdminTests
{
    private static IPlanLimitsService Limits()
    {
        var m = new Mock<IPlanLimitsService>();
        m.Setup(s => s.GetLimitsAsync(It.IsAny<TenantPlan>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((TenantPlan p, CancellationToken _) => PlanLimits.For(p));
        return m.Object;
    }

    // ── UpdateTenantPlan (plataforma) ───────────────────────────────────────────

    [Fact]
    public async Task UpdateTenantPlan_TenantNaoEncontrado_Falha()
    {
        var tenants = new Mock<ITenantRepository>();
        tenants.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((Tenant?)null);

        var handler = new UpdateTenantPlanCommandHandler(tenants.Object, new Mock<IUnitOfWork>().Object);
        var result = await handler.Handle(
            new UpdateTenantPlanCommand(Guid.NewGuid(), TenantCapability.Appointments, TenantPlan.Starter), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task UpdateTenantPlan_DefineCapacidadesEPlano()
    {
        var tenant = Tenant.Create("X", "x", TenantVertical.Other,
            capabilities: TenantCapability.Appointments | TenantCapability.Rentals, plan: TenantPlan.Free);
        var tenants = new Mock<ITenantRepository>();
        tenants.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateTenantPlanCommandHandler(tenants.Object, uow.Object);
        var result = await handler.Handle(
            new UpdateTenantPlanCommand(tenant.Id, TenantCapability.Rentals, TenantPlan.Professional), default);

        result.IsSuccess.Should().BeTrue();
        tenant.Has(TenantCapability.Rentals).Should().BeTrue();
        tenant.Has(TenantCapability.Appointments).Should().BeFalse();
        tenant.Plan.Should().Be(TenantPlan.Professional);
        tenants.Verify(r => r.Update(tenant), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetTenantUsage ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTenantUsage_RetornaUsoContraLimites()
    {
        var tenant = Tenant.Create("X", "x", TenantVertical.Other, plan: TenantPlan.Professional); // 20/3/20
        var plan = new Mock<ITenantPlanService>();
        plan.Setup(p => p.GetCurrentTenantAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Service, bool>>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(7);
        var resources = new Mock<IResourceRepository>();
        resources.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Resource, bool>>?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(2);
        var items = new Mock<IRentableItemRepository>();
        items.Setup(r => r.CountAsync(It.IsAny<Expression<Func<RentableItem, bool>>?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(5);

        var handler = new GetTenantUsageQueryHandler(plan.Object, Limits(), services.Object, resources.Object, items.Object);
        var result = await handler.Handle(new GetTenantUsageQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Services.Should().Be(new UsageItem(7, 20));
        result.Value.Resources.Should().Be(new UsageItem(2, 3));
        result.Value.RentableItems.Should().Be(new UsageItem(5, 20));
        result.Value.Plan.Should().Be(TenantPlan.Professional);
    }

    [Fact]
    public async Task GetTenantUsage_SemTenant_Falha()
    {
        var plan = new Mock<ITenantPlanService>();
        plan.Setup(p => p.GetCurrentTenantAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var handler = new GetTenantUsageQueryHandler(
            plan.Object, Limits(), new Mock<IServiceRepository>().Object,
            new Mock<IResourceRepository>().Object, new Mock<IRentableItemRepository>().Object);
        var result = await handler.Handle(new GetTenantUsageQuery(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
    }
}
