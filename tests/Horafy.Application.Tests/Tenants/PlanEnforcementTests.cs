using System.Linq.Expressions;
using FluentAssertions;
using Horafy.Application.Features.Rentals.Commands;
using Horafy.Application.Features.Resources.Commands;
using Horafy.Application.Features.Services.Commands;
using Horafy.Application.Features.Tenants;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public class PlanEnforcementTests
{
    private static Tenant TenantWith(TenantCapability caps, TenantPlan plan) =>
        Tenant.Create("Demo", "demo", TenantVertical.Other, capabilities: caps, plan: plan);

    private static ITenantPlanService Plan(Tenant? tenant)
    {
        var m = new Mock<ITenantPlanService>();
        m.Setup(s => s.GetCurrentTenantAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        return m.Object;
    }

    private static ITenantUnitOfWork Uow()
    {
        var m = new Mock<ITenantUnitOfWork>();
        m.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return m.Object;
    }

    // Limites = defaults do plano (sem override na tabela plan_configurations).
    private static IPlanLimitsService Limits()
    {
        var m = new Mock<IPlanLimitsService>();
        m.Setup(s => s.GetLimitsAsync(It.IsAny<TenantPlan>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((TenantPlan p, CancellationToken _) => PlanLimits.For(p));
        return m.Object;
    }

    // ── Serviço (capacidade Appointments) ───────────────────────────────────────

    [Fact]
    public async Task CreateService_SemCapacidadeAgendamento_Falha()
    {
        var repo = new Mock<IServiceRepository>();
        var tenant = TenantWith(TenantCapability.Rentals, TenantPlan.Professional); // só locação
        var handler = new CreateServiceCommandHandler(repo.Object, Plan(tenant), Limits(), Uow());

        var result = await handler.Handle(new CreateServiceCommand("Corte", 30, 50, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PlanErrors.AppointmentsNotEnabled);
        repo.Verify(r => r.Add(It.IsAny<Service>()), Times.Never);
    }

    [Fact]
    public async Task CreateService_LimiteAtingido_Falha()
    {
        var repo = new Mock<IServiceRepository>();
        repo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Service, bool>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5); // Free = 5 serviços
        var tenant = TenantWith(TenantCapability.Appointments, TenantPlan.Free);
        var handler = new CreateServiceCommandHandler(repo.Object, Plan(tenant), Limits(), Uow());

        var result = await handler.Handle(new CreateServiceCommand("Corte", 30, 50, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Plan.ServiceLimitReached");
        repo.Verify(r => r.Add(It.IsAny<Service>()), Times.Never);
    }

    [Fact]
    public async Task CreateService_DentroDoLimite_CriaComSucesso()
    {
        var repo = new Mock<IServiceRepository>();
        repo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Service, bool>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        repo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var tenant = TenantWith(TenantCapability.Appointments, TenantPlan.Free);
        var handler = new CreateServiceCommandHandler(repo.Object, Plan(tenant), Limits(), Uow());

        var result = await handler.Handle(new CreateServiceCommand("Corte", 30, 50, null, null), default);

        result.IsSuccess.Should().BeTrue();
        repo.Verify(r => r.Add(It.IsAny<Service>()), Times.Once);
    }

    // ── Recurso (capacidade Appointments) ───────────────────────────────────────

    [Fact]
    public async Task CreateResource_LimiteAtingido_Falha()
    {
        var repo = new Mock<IResourceRepository>();
        repo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Resource, bool>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2); // Free = 2 recursos
        var tenant = TenantWith(TenantCapability.Appointments, TenantPlan.Free);
        var handler = new CreateResourceCommandHandler(repo.Object, Plan(tenant), Limits(), Uow());

        var result = await handler.Handle(
            new CreateResourceCommand("Sala", ResourceType.PhysicalSpace, null, null, null, null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Plan.ResourceLimitReached");
    }

    // ── Item de locação (capacidade Rentals) ────────────────────────────────────

    [Fact]
    public async Task CreateRentableItem_SemCapacidadeLocacao_Falha()
    {
        var repo = new Mock<IRentableItemRepository>();
        var tenant = TenantWith(TenantCapability.Appointments, TenantPlan.Professional); // só agendamento
        var handler = new CreateRentableItemCommandHandler(repo.Object, Plan(tenant), Limits(), Uow());

        var result = await handler.Handle(
            new CreateRentableItemCommand("Furadeira", 1, 30, 50, 0, null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PlanErrors.RentalsNotEnabled);
        repo.Verify(r => r.Add(It.IsAny<RentableItem>()), Times.Never);
    }

    [Fact]
    public async Task CreateRentableItem_LimiteAtingido_Falha()
    {
        var repo = new Mock<IRentableItemRepository>();
        repo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<RentableItem, bool>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(20); // Professional = 20 itens
        var tenant = TenantWith(TenantCapability.Rentals, TenantPlan.Professional);
        var handler = new CreateRentableItemCommandHandler(repo.Object, Plan(tenant), Limits(), Uow());

        var result = await handler.Handle(
            new CreateRentableItemCommand("Furadeira", 1, 30, 50, 0, null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Plan.RentableItemLimitReached");
    }

    [Fact]
    public async Task CreateRentableItem_DentroDoLimite_CriaComSucesso()
    {
        var repo = new Mock<IRentableItemRepository>();
        repo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<RentableItem, bool>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var tenant = TenantWith(TenantCapability.Rentals, TenantPlan.Professional);
        var handler = new CreateRentableItemCommandHandler(repo.Object, Plan(tenant), Limits(), Uow());

        var result = await handler.Handle(
            new CreateRentableItemCommand("Furadeira", 1, 30, 50, 0, null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        repo.Verify(r => r.Add(It.IsAny<RentableItem>()), Times.Once);
    }

    // ── Sem tenant resolvido → não bloqueia (compatibilidade) ────────────────────

    [Fact]
    public async Task CreateService_SemTenantResolvido_NaoBloqueia()
    {
        var repo = new Mock<IServiceRepository>();
        repo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new CreateServiceCommandHandler(repo.Object, Plan(null), Limits(), Uow());

        var result = await handler.Handle(new CreateServiceCommand("Corte", 30, 50, null, null), default);

        result.IsSuccess.Should().BeTrue();
    }
}
