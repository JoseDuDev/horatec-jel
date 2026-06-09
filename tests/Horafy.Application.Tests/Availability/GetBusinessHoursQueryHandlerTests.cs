using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public sealed class GetBusinessHoursQueryHandlerTests
{
    private readonly Mock<IAvailabilityRepository> _repo = new();

    private GetBusinessHoursQueryHandler MakeHandler() =>
        new(_repo.Object);

    [Fact]
    public async Task Handle_NenhumHorarioCadastrado_RetornaSeteDiasComPadraoFechado()
    {
        _repo.Setup(r => r.GetBusinessHoursAsync(default))
            .ReturnsAsync(new List<BusinessHours>());

        var result = await MakeHandler().Handle(new GetBusinessHoursQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(7);
        result.Value.Should().AllSatisfy(bh => bh.IsOpen.Should().BeFalse());
    }

    [Fact]
    public async Task Handle_HorarioSegundaCadastrado_RetornaValoresCadastradosParaSegunda()
    {
        var segunda = BusinessHours.Create(
            DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0), isOpen: true);
        _repo.Setup(r => r.GetBusinessHoursAsync(default))
            .ReturnsAsync(new List<BusinessHours> { segunda });

        var result = await MakeHandler().Handle(new GetBusinessHoursQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(7);

        var seg = result.Value.First(b => b.DayOfWeek == DayOfWeek.Monday);
        seg.IsOpen.Should().BeTrue();
        seg.OpenTime.Should().Be(new TimeOnly(8, 0));
        seg.CloseTime.Should().Be(new TimeOnly(17, 0));

        result.Value.Where(b => b.DayOfWeek != DayOfWeek.Monday)
            .Should().AllSatisfy(b => b.IsOpen.Should().BeFalse());
    }
}
