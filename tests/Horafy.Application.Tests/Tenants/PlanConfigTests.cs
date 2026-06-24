using FluentAssertions;
using Horafy.Application.Features.Tenants.Commands.UpdatePlanConfig;
using Horafy.Application.Features.Tenants.Queries.GetPlans;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.MultiTenancy;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public class PlanConfigTests
{
    // ── PlanLimitsService: config persistida sobrepõe o default ──────────────────

    [Fact]
    public async Task GetLimits_ComConfigPersistida_UsaConfig()
    {
        var repo = new Mock<IPlanConfigurationRepository>();
        repo.Setup(r => r.GetByPlanAsync(TenantPlan.Free, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlanConfiguration.Create(TenantPlan.Free, 99, 88, 77));

        var limits = await new PlanLimitsService(repo.Object).GetLimitsAsync(TenantPlan.Free);

        limits.Should().Be(new PlanLimits(99, 88, 77));
    }

    [Fact]
    public async Task GetLimits_SemConfig_UsaDefault()
    {
        var repo = new Mock<IPlanConfigurationRepository>();
        repo.Setup(r => r.GetByPlanAsync(It.IsAny<TenantPlan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlanConfiguration?)null);

        var limits = await new PlanLimitsService(repo.Object).GetLimitsAsync(TenantPlan.Professional);

        limits.Should().Be(PlanLimits.For(TenantPlan.Professional)); // 20/3/20
    }

    // ── UpdatePlanConfig: upsert ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePlanConfig_SemConfigExistente_Cria()
    {
        var repo = new Mock<IPlanConfigurationRepository>();
        repo.Setup(r => r.GetByPlanAsync(TenantPlan.Starter, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlanConfiguration?)null);
        PlanConfiguration? added = null;
        repo.Setup(r => r.Add(It.IsAny<PlanConfiguration>())).Callback<PlanConfiguration>(c => added = c);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdatePlanConfigCommandHandler(repo.Object, uow.Object);
        var result = await handler.Handle(new UpdatePlanConfigCommand(TenantPlan.Starter, 15, 4, 15), default);

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.ToLimits().Should().Be(new PlanLimits(15, 4, 15));
    }

    [Fact]
    public async Task UpdatePlanConfig_ConfigExistente_Atualiza()
    {
        var existing = PlanConfiguration.Create(TenantPlan.Starter, 10, 2, 10);
        var repo = new Mock<IPlanConfigurationRepository>();
        repo.Setup(r => r.GetByPlanAsync(TenantPlan.Starter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdatePlanConfigCommandHandler(repo.Object, uow.Object);
        var result = await handler.Handle(new UpdatePlanConfigCommand(TenantPlan.Starter, 30, 9, 30), default);

        result.IsSuccess.Should().BeTrue();
        existing.ToLimits().Should().Be(new PlanLimits(30, 9, 30));
        repo.Verify(r => r.Update(existing), Times.Once);
    }

    // ── GetPlans ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlans_RetornaTodosOsPlanosComLimitesEfetivos()
    {
        var planLimits = new Mock<IPlanLimitsService>();
        planLimits.Setup(s => s.GetLimitsAsync(It.IsAny<TenantPlan>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((TenantPlan p, CancellationToken _) => PlanLimits.For(p));

        var handler = new GetPlansQueryHandler(planLimits.Object);
        var result = await handler.Handle(new GetPlansQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);
        result.Value.Single(p => p.Plan == TenantPlan.Professional).MaxServices.Should().Be(20);
    }
}
