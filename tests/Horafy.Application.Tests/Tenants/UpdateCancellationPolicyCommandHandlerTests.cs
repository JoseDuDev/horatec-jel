using FluentAssertions;
using Horafy.Application.Features.Tenants.Commands.UpdateCancellationPolicy;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public sealed class UpdateCancellationPolicyCommandHandlerTests
{
    private readonly Mock<ITenantRepository>     _repo      = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc = new();
    private readonly Mock<IUnitOfWork>           _uow       = new();

    private UpdateCancellationPolicyCommandHandler MakeHandler() =>
        new(_repo.Object, _tenantSvc.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidPolicy_UpdatesCancellationPolicyAndSaves()
    {
        var tenantId = Guid.NewGuid();
        var tenant   = Tenant.Create("T", "t", TenantVertical.Barbershop);

        _tenantSvc.Setup(s => s.TenantId).Returns(tenantId);
        _repo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var result = await MakeHandler().Handle(
            new UpdateCancellationPolicyCommand(24, 20m, false), default);

        result.IsSuccess.Should().BeTrue();
        tenant.CancellationPolicy.MinCancellationHours.Should().Be(24);
        tenant.CancellationPolicy.CancellationFeePercent.Should().Be(20m);
        tenant.CancellationPolicy.AllowCustomerCancellation.Should().BeFalse();
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_NoTenantContext_ReturnsUnauthorized()
    {
        _tenantSvc.Setup(s => s.TenantId).Returns((Guid?)null);

        var result = await MakeHandler().Handle(
            new UpdateCancellationPolicyCommand(24, 0m, true), default);

        result.IsFailure.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
