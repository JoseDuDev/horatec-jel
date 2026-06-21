using FluentAssertions;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using Moq;
using System.Data;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public sealed class CreateRecurringBookingCommandHandlerTests
{
    private readonly Mock<IServiceRepository>  _serviceRepo  = new();
    private readonly Mock<IResourceRepository> _resourceRepo = new();
    private readonly Mock<IBookingRepository>  _bookingRepo  = new();
    private readonly Mock<ICurrentUserService> _currentUser  = new();
    private readonly Mock<ITenantUnitOfWork>   _unitOfWork   = new();

    private CreateRecurringBookingCommandHandler MakeHandler() =>
        new(_serviceRepo.Object, _resourceRepo.Object,
            _bookingRepo.Object, _currentUser.Object, _unitOfWork.Object);

    private void SetupDefaults(Service service, Resource resource, Guid userId)
    {
        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _bookingRepo.Setup(r => r.HasConflictAsync(
            It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), null, default)).ReturnsAsync(false);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Email).Returns("cliente@test.com");
        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<Result<Guid>>>>(),
                It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
           .Returns((Func<CancellationToken, Task<Result<Guid>>> op, IsolationLevel _, CancellationToken ct) => op(ct));
    }

    [Fact]
    public async Task Handle_Weekly3Occurrences_Creates3Bookings()
    {
        var service  = Service.Create("Corte", 60, 50m);
        var resource = Resource.Create("João", ResourceType.Professional);
        var userId   = Guid.NewGuid();
        SetupDefaults(service, resource, userId);

        var cmd = new CreateRecurringBookingCommand(
            service.Id, resource.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            RecurrenceFrequency.Weekly, OccurrenceCount: 3, Notes: null);

        var result = await MakeHandler().Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _bookingRepo.Verify(r => r.Add(It.IsAny<Booking>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_ConflictOnSecondOccurrence_ReturnsConflictError()
    {
        var service  = Service.Create("Corte", 60, 50m);
        var resource = Resource.Create("João", ResourceType.Professional);
        var userId   = Guid.NewGuid();
        var first    = DateTimeOffset.UtcNow.AddDays(1);
        var second   = first.AddDays(7);

        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Email).Returns("cliente@test.com");

        _bookingRepo.Setup(r => r.HasConflictAsync(
            resource.Id, first, first.AddMinutes(60), null, default)).ReturnsAsync(false);
        _bookingRepo.Setup(r => r.HasConflictAsync(
            resource.Id, second, second.AddMinutes(60), null, default)).ReturnsAsync(true);

        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<Result<Guid>>>>(),
                It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
           .Returns((Func<CancellationToken, Task<Result<Guid>>> op, IsolationLevel _, CancellationToken ct) => op(ct));

        var cmd = new CreateRecurringBookingCommand(
            service.Id, resource.Id, first,
            RecurrenceFrequency.Weekly, OccurrenceCount: 2, Notes: null);

        var result = await MakeHandler().Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.Conflict");
    }
}
