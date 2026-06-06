using FluentAssertions;
using Horafy.Application.Features.Favorites.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Favorites;

public sealed class AddFavoriteServiceCommandTests
{
    private readonly Mock<ICurrentUserService>        _currentUser = new();
    private readonly Mock<IFavoriteServiceRepository> _repo        = new();
    private readonly Mock<ITenantUnitOfWork>          _uow         = new();

    private AddFavoriteServiceCommandHandler MakeHandler() =>
        new(_currentUser.Object, _repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_NotYetFavorited_AddsFavorite()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)customerId);
        _repo.Setup(r => r.GetAsync(customerId, serviceId, default))
            .ReturnsAsync((FavoriteService?)null);

        var result = await MakeHandler().Handle(
            new AddFavoriteServiceCommand(serviceId), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Add(It.IsAny<FavoriteService>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyFavorited_ReturnsConflict()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)customerId);

        var existing = FavoriteService.Create(customerId, serviceId);
        _repo.Setup(r => r.GetAsync(customerId, serviceId, default)).ReturnsAsync(existing);

        var result = await MakeHandler().Handle(
            new AddFavoriteServiceCommand(serviceId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Favorite.AlreadyExists");
    }
}
