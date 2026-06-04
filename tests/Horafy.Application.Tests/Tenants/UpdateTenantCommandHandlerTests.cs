using FluentAssertions;
using Horafy.Application.Features.Tenants.Commands.UpdateTenant;
using Horafy.Application.Features.Tenants.Commands.SetCustomDomain;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public class UpdateTenantCommandHandlerTests
{
    private readonly Mock<ICurrentTenantService> _currentTenant  = new();
    private readonly Mock<ITenantRepository>     _tenantRepo     = new();
    private readonly Mock<IUnitOfWork>           _unitOfWork     = new();

    private static Tenant MakeTenant() =>
        Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop, "bar@test.com");

    // ── UpdateTenant ──────────────────────────────────────────────────
    [Fact]
    public async Task UpdateTenant_ValidRequest_ReturnsSuccess()
    {
        var tenant   = MakeTenant();
        var tenantId = tenant.Id;

        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var handler = new UpdateTenantCommandHandler(
            _currentTenant.Object, _tenantRepo.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new UpdateTenantCommand("Nova Barbearia", "new@email.com",
                "11999", "Rua A", "SP", "SP", "01000-000", null, null),
            default);

        result.IsSuccess.Should().BeTrue();
        _tenantRepo.Verify(r => r.Update(It.Is<Tenant>(t => t.Name == "Nova Barbearia")), Times.Once);
    }

    [Fact]
    public async Task UpdateTenant_NoTenantContext_ReturnsUnauthorized()
    {
        _currentTenant.SetupGet(t => t.TenantId).Returns((Guid?)null);

        var handler = new UpdateTenantCommandHandler(
            _currentTenant.Object, _tenantRepo.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new UpdateTenantCommand("Nome", null, null, null, null, null, null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(Horafy.Shared.ErrorType.Unauthorized);
    }

    // ── SetCustomDomain ───────────────────────────────────────────────
    [Fact]
    public async Task SetCustomDomain_DomainAlreadyTaken_ReturnsConflict()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.IsDomainTakenAsync(
            "minha.com.br", tenantId, default)).ReturnsAsync(true);

        var handler = new SetCustomDomainCommandHandler(
            _currentTenant.Object, _tenantRepo.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new SetCustomDomainCommand("minha.com.br"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.DomainAlreadyTaken");
    }

    [Fact]
    public async Task SetCustomDomain_Available_SetsAndSaves()
    {
        var tenant   = MakeTenant();
        var tenantId = tenant.Id;

        _currentTenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantRepo.Setup(r => r.IsDomainTakenAsync(
            "nova.com.br", tenantId, default)).ReturnsAsync(false);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var handler = new SetCustomDomainCommandHandler(
            _currentTenant.Object, _tenantRepo.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new SetCustomDomainCommand("nova.com.br"), default);

        result.IsSuccess.Should().BeTrue();
        _tenantRepo.Verify(r => r.Update(It.Is<Tenant>(
            t => t.CustomDomain == "nova.com.br")), Times.Once);
    }
}
