using FluentAssertions;
using Horafy.Application.Features.Tenants.Commands.UpdateLoyaltySettings;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public sealed class UpdateLoyaltySettingsCommandHandlerTests
{
    private readonly Mock<ITenantRepository>     _repo      = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc = new();
    private readonly Mock<IUnitOfWork>           _uow       = new();

    private UpdateLoyaltySettingsCommandHandler MakeHandler() =>
        new(_repo.Object, _tenantSvc.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidSettings_UpdatesLoyaltyAndSaves()
    {
        var tenantId = Guid.NewGuid();
        var tenant   = Tenant.Create("T", "t", TenantVertical.Barbershop);

        _tenantSvc.Setup(s => s.TenantId).Returns(tenantId);
        _repo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(
            new UpdateLoyaltySettingsCommand(true, 10m, 50m), default);

        result.IsSuccess.Should().BeTrue();
        tenant.LoyaltySettings.IsEnabled.Should().BeTrue();
        tenant.LoyaltySettings.CreditRatePercent.Should().Be(10m);
        tenant.LoyaltySettings.MinBookingAmount.Should().Be(50m);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_NoTenantContext_ReturnsUnauthorized()
    {
        _tenantSvc.Setup(s => s.TenantId).Returns((Guid?)null);

        var result = await MakeHandler().Handle(
            new UpdateLoyaltySettingsCommand(true, 10m, 0m), default);

        result.IsFailure.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
