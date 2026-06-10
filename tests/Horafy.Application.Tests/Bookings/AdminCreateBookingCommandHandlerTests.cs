using FluentAssertions;
using Horafy.Application.Features.Bookings;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public sealed class AdminCreateBookingCommandHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private static readonly DateTimeOffset FutureSlot = DateTimeOffset.UtcNow.AddDays(1);

    private readonly Mock<IServiceRepository>  _services  = new();
    private readonly Mock<IResourceRepository> _resources = new();
    private readonly Mock<IBookingRepository>  _bookings  = new();
    private readonly Mock<ITenantUnitOfWork>   _uow       = new();

    private AdminCreateBookingCommandHandler MakeHandler() =>
        new(_services.Object, _resources.Object, _bookings.Object, _uow.Object);

    private static Resource MakeResource() =>
        Resource.Create("Sala 1", ResourceType.Professional);

    private static Service MakeService() =>
        Service.Create("Corte", 60, 50m);

    [Fact]
    public async Task Handle_RecursoNaoEncontrado_RetornaFalha()
    {
        _resources.Setup(r => r.GetByIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Resource?)null);

        var result = await MakeHandler().Handle(
            new AdminCreateBookingCommand(
                [Guid.NewGuid()], ResourceId, FutureSlot,
                "João Silva", null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.ResourceNotFound);
        _services.Verify(
            s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ServicoNaoEncontrado_RetornaFalha()
    {
        _resources.Setup(r => r.GetByIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResource());
        _services.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Service>());

        var result = await MakeHandler().Handle(
            new AdminCreateBookingCommand(
                [Guid.NewGuid()], ResourceId, FutureSlot,
                "João Silva", null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.ServiceNotFound);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HorarioComConflito_RetornaFalha()
    {
        var service = MakeService();
        _resources.Setup(r => r.GetByIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResource());
        _services.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Service> { service });
        _bookings.Setup(b => b.HasConflictAsync(
                ResourceId,
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await MakeHandler().Handle(
            new AdminCreateBookingCommand(
                [service.Id], ResourceId, FutureSlot,
                "João Silva", null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.Conflict);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DadosValidos_CriaAgendamentoESalva()
    {
        var service = MakeService();
        _resources.Setup(r => r.GetByIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResource());
        _services.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Service> { service });
        _bookings.Setup(b => b.HasConflictAsync(
                ResourceId,
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await MakeHandler().Handle(
            new AdminCreateBookingCommand(
                [service.Id], ResourceId, FutureSlot,
                "João Silva", "joao@email.com", "11999999999", "Obs"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _bookings.Verify(b => b.Add(It.IsAny<Booking>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
