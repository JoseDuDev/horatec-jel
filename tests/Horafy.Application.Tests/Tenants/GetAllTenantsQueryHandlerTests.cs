using FluentAssertions;
using Horafy.Application.Features.Tenants.Queries.GetAllTenants;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Tenants;

public sealed class GetAllTenantsQueryHandlerTests
{
    private readonly Mock<ITenantRepository> _repo = new();

    private GetAllTenantsQueryHandler MakeHandler() => new(_repo.Object);

    [Fact]
    public async Task Handle_WithTenants_ReturnsSummaryList()
    {
        var t1 = Tenant.Create("Barbearia A", "barb-a", TenantVertical.Barbershop, "a@test.com");
        var t2 = Tenant.Create("Clínica B", "clinic-b", TenantVertical.MedicalClinic, "b@test.com");

        _repo.Setup(r => r.GetAllAsync(default))
             .ReturnsAsync(new List<Tenant> { t1, t2 });

        var result = await MakeHandler().Handle(new GetAllTenantsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(s => s.Slug).Should().Contain("barb-a").And.Contain("clinic-b");
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyList()
    {
        _repo.Setup(r => r.GetAllAsync(default))
             .ReturnsAsync(new List<Tenant>());

        var result = await MakeHandler().Handle(new GetAllTenantsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsAllFieldsCorrectly()
    {
        var tenant = Tenant.Create("Loja X", "loja-x", TenantVertical.Other, "x@test.com");

        _repo.Setup(r => r.GetAllAsync(default))
             .ReturnsAsync(new List<Tenant> { tenant });

        var result = await MakeHandler().Handle(new GetAllTenantsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var summary = result.Value.Single();
        summary.Name.Should().Be("Loja X");
        summary.Slug.Should().Be("loja-x");
        summary.Vertical.Should().Be(TenantVertical.Other);
        summary.Email.Should().Be("x@test.com");
        summary.Status.Should().Be(TenantStatus.Active);
        summary.Plan.Should().Be(TenantPlan.Free);
    }
}
