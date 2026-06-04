using FluentAssertions;
using Horafy.Application.Features.Waitlist.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Waitlist;

public sealed class JoinWaitlistCommandHandlerTests
{
    private readonly Mock<IWaitlistRepository>  _waitlistRepo = new();
    private readonly Mock<IServiceRepository>   _serviceRepo  = new();
    private readonly Mock<IResourceRepository>  _resourceRepo = new();
    private readonly Mock<ICurrentUserService>  _currentUser  = new();
    private readonly Mock<ITenantUnitOfWork>    _unitOfWork   = new();

    private JoinWaitlistCommandHandler MakeHandler() =>
        new(_waitlistRepo.Object, _serviceRepo.Object, _resourceRepo.Object,
            _currentUser.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_ValidRequest_ReturnsEntryId()
    {
        var service  = Service.Create("Corte", 60, 50m);
        var resource = Resource.Create("João", ResourceType.Professional);
        var userId   = Guid.NewGuid();
        var date     = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _waitlistRepo.Setup(r => r.ExistsActiveAsync(
            service.Id, resource.Id, userId, date, default)).ReturnsAsync(false);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);
        _currentUser.SetupGet(u => u.Email).Returns("c@test.com");

        var result = await MakeHandler().Handle(
            new JoinWaitlistCommand(service.Id, resource.Id, date), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_AlreadyInQueue_ReturnsConflict()
    {
        var service  = Service.Create("Corte", 60, 50m);
        var resource = Resource.Create("João", ResourceType.Professional);
        var userId   = Guid.NewGuid();
        var date     = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

        _serviceRepo.Setup(r => r.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _resourceRepo.Setup(r => r.GetByIdAsync(resource.Id, default)).ReturnsAsync(resource);
        _waitlistRepo.Setup(r => r.ExistsActiveAsync(
            service.Id, resource.Id, userId, date, default)).ReturnsAsync(true);
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId);

        var result = await MakeHandler().Handle(
            new JoinWaitlistCommand(service.Id, resource.Id, date), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Waitlist.AlreadyInQueue");
    }
}
