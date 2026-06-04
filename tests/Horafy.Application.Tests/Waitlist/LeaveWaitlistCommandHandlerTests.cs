using FluentAssertions;
using Horafy.Application.Features.Waitlist.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Waitlist;

public sealed class LeaveWaitlistCommandHandlerTests
{
    private readonly Mock<IWaitlistRepository> _waitlistRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser  = new();
    private readonly Mock<ITenantUnitOfWork>   _unitOfWork   = new();

    private LeaveWaitlistCommandHandler MakeHandler() =>
        new(_waitlistRepo.Object, _currentUser.Object, _unitOfWork.Object);

    private static WaitlistEntry MakeEntry(Guid customerId) =>
        WaitlistEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), customerId,
            "Cliente", "cliente@test.com",
            DateOnly.FromDateTime(DateTime.Today.AddDays(3)));

    [Fact]
    public async Task Handle_AuthenticatedOwner_ReturnsSuccessAndCancelsEntry()
    {
        var userId = Guid.NewGuid();
        var entry  = MakeEntry(userId);

        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _waitlistRepo.Setup(r => r.GetByIdAsync(entry.Id, default)).ReturnsAsync(entry);

        var result = await MakeHandler().Handle(new LeaveWaitlistCommand(entry.Id), default);

        result.IsSuccess.Should().BeTrue();
        entry.Status.Should().Be(WaitlistStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_EntryNotFound_ReturnsNotFoundError()
    {
        var userId = Guid.NewGuid();

        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _waitlistRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                     .ReturnsAsync((WaitlistEntry?)null);

        var result = await MakeHandler().Handle(new LeaveWaitlistCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Waitlist.NotFound");
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ReturnsUnauthorizedError()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);
        _currentUser.SetupGet(u => u.UserId).Returns((Guid?)null);

        var result = await MakeHandler().Handle(new LeaveWaitlistCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("General.Unauthorized");
    }

    [Fact]
    public async Task Handle_DifferentUser_ReturnsUnauthorizedError()
    {
        var ownerId    = Guid.NewGuid();
        var callerId   = Guid.NewGuid();
        var entry      = MakeEntry(ownerId);

        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(callerId);
        _waitlistRepo.Setup(r => r.GetByIdAsync(entry.Id, default)).ReturnsAsync(entry);

        var result = await MakeHandler().Handle(new LeaveWaitlistCommand(entry.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("General.Unauthorized");
    }
}
