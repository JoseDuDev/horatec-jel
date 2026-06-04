using FluentAssertions;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Professionals;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public class CreateBookingCommandHandlerTests
{
    private readonly Mock<IServiceRepository>      _serviceRepo      = new();
    private readonly Mock<IProfessionalRepository> _professionalRepo = new();
    private readonly Mock<IBookingRepository>      _bookingRepo      = new();
    private readonly Mock<ICurrentUserService>     _currentUser      = new();
    private readonly Mock<ITenantUnitOfWork>       _unitOfWork       = new();

    private CreateBookingCommandHandler CreateHandler() =>
        new(_serviceRepo.Object, _professionalRepo.Object,
            _bookingRepo.Object, _currentUser.Object, _unitOfWork.Object);

    private static Service MakeService() =>
        Service.Create("Corte", 60, 50m);

    private static Professional MakeProfessional() =>
        Professional.Create("João");

    // ── Cenário: sucesso ──────────────────────────────────────────────
    [Fact]
    public async Task Handle_ValidRequest_ReturnsBookingId()
    {
        var service      = MakeService();
        var professional = MakeProfessional();
        var userId       = Guid.NewGuid();
        var scheduled    = DateTimeOffset.UtcNow.AddHours(2);

        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _professionalRepo.Setup(r => r.GetByIdAsync(professional.Id, default)).ReturnsAsync(professional);
        _bookingRepo.Setup(r => r.HasConflictAsync(professional.Id, scheduled,
            scheduled.AddMinutes(60), null, default)).ReturnsAsync(false);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Email).Returns("cliente@test.com");

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(service.Id, professional.Id, scheduled, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    // ── Cenário: serviço não encontrado ───────────────────────────────
    [Fact]
    public async Task Handle_ServiceNotFound_ReturnsError()
    {
        _serviceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                    .ReturnsAsync((Service?)null);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(Guid.NewGuid(), Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddHours(1), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.ServiceNotFound");
    }

    // ── Cenário: profissional não encontrado ──────────────────────────
    [Fact]
    public async Task Handle_ProfessionalNotFound_ReturnsError()
    {
        var service = MakeService();
        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _professionalRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                         .ReturnsAsync((Professional?)null);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(service.Id, Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddHours(1), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.ProfessionalNotFound");
    }

    // ── Cenário: conflito de horário ──────────────────────────────────
    [Fact]
    public async Task Handle_TimeConflict_ReturnsConflictError()
    {
        var service      = MakeService();
        var professional = MakeProfessional();
        var scheduled    = DateTimeOffset.UtcNow.AddHours(2);

        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _professionalRepo.Setup(r => r.GetByIdAsync(professional.Id, default)).ReturnsAsync(professional);
        _bookingRepo.Setup(r => r.HasConflictAsync(
            professional.Id, scheduled, scheduled.AddMinutes(60), null, default)).ReturnsAsync(true);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(service.Id, professional.Id, scheduled, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.Conflict");
    }

    // ── Cenário: usuário não autenticado ──────────────────────────────
    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsUnauthorized()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);
        _currentUser.SetupGet(u => u.UserId).Returns((Guid?)null);

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(Guid.NewGuid(), Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddHours(1), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(Horafy.Shared.ErrorType.Unauthorized);
    }
}
