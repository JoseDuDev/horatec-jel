using FluentAssertions;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public sealed class CreateBookingCommandHandlerTests
{
    private readonly Mock<IServiceRepository>  _serviceRepo  = new();
    private readonly Mock<IResourceRepository> _resourceRepo = new();
    private readonly Mock<IBookingRepository>  _bookingRepo  = new();
    private readonly Mock<ICurrentUserService> _currentUser  = new();
    private readonly Mock<ITenantUnitOfWork>   _unitOfWork   = new();

    private CreateBookingCommandHandler CreateHandler() =>
        new(_serviceRepo.Object, _resourceRepo.Object,
            _bookingRepo.Object, _currentUser.Object, _unitOfWork.Object);

    private static Service MakeService() =>
        Service.Create("Corte", 60, 50m);

    private static Resource MakeResource() =>
        Resource.Create("João", ResourceType.Professional);

    [Fact]
    public async Task Handle_ValidRequest_ReturnsBookingId()
    {
        var service   = MakeService();
        var resource  = MakeResource();
        var userId    = Guid.NewGuid();
        var scheduled = DateTimeOffset.UtcNow.AddHours(2);

        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _serviceRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
                    .ReturnsAsync(new List<Service> { service });
        _bookingRepo.Setup(r => r.HasConflictAsync(
            resource.Id, scheduled, scheduled.AddMinutes(60), It.IsAny<Guid?>(), default)).ReturnsAsync(false);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Email).Returns("cliente@test.com");

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(new[] { service.Id }, resource.Id, scheduled, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ServiceNotFound_ReturnsError()
    {
        var resource = MakeResource();
        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _serviceRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
                    .ReturnsAsync(new List<Service>());
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(new[] { Guid.NewGuid() }, resource.Id,
                DateTimeOffset.UtcNow.AddHours(1), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.ServiceNotFound");
    }

    [Fact]
    public async Task Handle_ResourceNotFound_ReturnsError()
    {
        var service = MakeService();
        _resourceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                     .ReturnsAsync((Resource?)null);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(new[] { service.Id }, Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddHours(1), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.ResourceNotFound");
    }

    [Fact]
    public async Task Handle_TimeConflict_ReturnsConflictError()
    {
        var service   = MakeService();
        var resource  = MakeResource();
        var scheduled = DateTimeOffset.UtcNow.AddHours(2);

        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _serviceRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
                    .ReturnsAsync(new List<Service> { service });
        _bookingRepo.Setup(r => r.HasConflictAsync(
            resource.Id, scheduled, scheduled.AddMinutes(60), It.IsAny<Guid?>(), default)).ReturnsAsync(true);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(new[] { service.Id }, resource.Id, scheduled, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.Conflict");
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsUnauthorized()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);
        _currentUser.SetupGet(u => u.UserId).Returns((Guid?)null);

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(new[] { Guid.NewGuid() }, Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddHours(1), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(Horafy.Shared.ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_MultipleServices_UsesTotalDurationForConflictCheck()
    {
        var service1  = MakeService();
        var service2  = Service.Create("Escova", 30, 30m);
        var resource  = MakeResource();
        var userId    = Guid.NewGuid();
        var scheduled = DateTimeOffset.UtcNow.AddHours(2);
        var expectedEnd = scheduled.AddMinutes(90); // 60 + 30

        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _serviceRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
                    .ReturnsAsync(new List<Service> { service1, service2 });
        _bookingRepo.Setup(r => r.HasConflictAsync(
            resource.Id, scheduled, expectedEnd, It.IsAny<Guid?>(), default)).ReturnsAsync(false);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Email).Returns("cliente@test.com");

        var result = await CreateHandler().Handle(
            new CreateBookingCommand(new[] { service1.Id, service2.Id }, resource.Id, scheduled, null), default);

        result.IsSuccess.Should().BeTrue();
    }
}
