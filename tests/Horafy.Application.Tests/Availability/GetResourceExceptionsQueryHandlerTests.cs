using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public sealed class GetResourceExceptionsQueryHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private static readonly DateOnly From = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly To = From.AddDays(30);

    private readonly Mock<IAvailabilityRepository> _repo = new();

    private GetResourceExceptionsQueryHandler MakeHandler() =>
        new(_repo.Object);

    [Fact]
    public async Task Handle_SemExcecoes_RetornaListaVazia()
    {
        _repo.Setup(r => r.GetExceptionsByResourceAsync(ResourceId, From, To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailabilityException>());

        var result = await MakeHandler().Handle(
            new GetResourceExceptionsQuery(ResourceId, From, To), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ComExcecaoBloqueada_RetornaDadosMapeados()
    {
        var date = From.AddDays(5);
        var excecao = AvailabilityException.CreateBlock(ResourceId, date, "Feriado Nacional");
        _repo.Setup(r => r.GetExceptionsByResourceAsync(ResourceId, From, To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailabilityException> { excecao });

        var result = await MakeHandler().Handle(
            new GetResourceExceptionsQuery(ResourceId, From, To), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Date.Should().Be(date);
        result.Value[0].IsBlocked.Should().BeTrue();
        result.Value[0].Reason.Should().Be("Feriado Nacional");
        result.Value[0].CustomStart.Should().BeNull();
        result.Value[0].CustomEnd.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ComExcecaoHorarioCustom_RetornaHorarioCustom()
    {
        var date = From.AddDays(3);
        var excecao = AvailabilityException.CreateCustomHours(
            ResourceId, date, new TimeOnly(10, 0), new TimeOnly(14, 0), "Expediente reduzido");
        _repo.Setup(r => r.GetExceptionsByResourceAsync(ResourceId, From, To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailabilityException> { excecao });

        var result = await MakeHandler().Handle(
            new GetResourceExceptionsQuery(ResourceId, From, To), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].IsBlocked.Should().BeFalse();
        result.Value[0].CustomStart.Should().Be(new TimeOnly(10, 0));
        result.Value[0].CustomEnd.Should().Be(new TimeOnly(14, 0));
    }
}
