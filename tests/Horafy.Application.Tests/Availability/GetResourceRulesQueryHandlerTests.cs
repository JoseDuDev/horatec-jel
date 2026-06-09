using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public sealed class GetResourceRulesQueryHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private readonly Mock<IAvailabilityRepository> _repo = new();

    private GetResourceRulesQueryHandler MakeHandler() =>
        new(_repo.Object);

    [Fact]
    public async Task Handle_SemRegras_RetornaListaVazia()
    {
        _repo.Setup(r => r.GetRulesByResourceAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailabilityRule>());

        var result = await MakeHandler().Handle(new GetResourceRulesQuery(ResourceId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ComRegra_RetornaDadosMapeados()
    {
        var rule = AvailabilityRule.Create(
            ResourceId, DayOfWeek.Monday,
            new TimeOnly(9, 0), new TimeOnly(17, 0), 60, breakAfterMinutes: 10);
        _repo.Setup(r => r.GetRulesByResourceAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailabilityRule> { rule });

        var result = await MakeHandler().Handle(new GetResourceRulesQuery(ResourceId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.Value[0].StartTime.Should().Be(new TimeOnly(9, 0));
        result.Value[0].EndTime.Should().Be(new TimeOnly(17, 0));
        result.Value[0].SlotDurationMinutes.Should().Be(60);
        result.Value[0].BreakAfterMinutes.Should().Be(10);
    }
}
