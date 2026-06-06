using FluentAssertions;
using Horafy.Application.Features.Favorites.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Favorites;

public sealed class RemoveFavoriteServiceCommandTests
{
    private readonly Mock<ICurrentUserService>        _currentUser = new();
    private readonly Mock<IFavoriteServiceRepository> _repo        = new();
    private readonly Mock<ITenantUnitOfWork>          _uow         = new();

    private RemoveFavoriteServiceCommandHandler MakeHandler() =>
        new(_currentUser.Object, _repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_ExistingFavorite_RemovesIt()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)customerId);

        var existing = FavoriteService.Create(customerId, serviceId);
        _repo.Setup(r => r.GetAsync(customerId, serviceId, default)).ReturnsAsync(existing);

        var result = await MakeHandler().Handle(
            new RemoveFavoriteServiceCommand(serviceId), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Remove(existing), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_NotFavorited_ReturnsNotFound()
    {
        _currentUser.Setup(c => c.UserId).Returns((Guid?)Guid.NewGuid());
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
            .ReturnsAsync((FavoriteService?)null);

        var result = await MakeHandler().Handle(
            new RemoveFavoriteServiceCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Favorite.NotFound");
    }
}
