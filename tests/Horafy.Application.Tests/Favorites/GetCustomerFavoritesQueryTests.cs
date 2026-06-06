using FluentAssertions;
using Horafy.Application.Features.Favorites.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Favorites;

public sealed class GetCustomerFavoritesQueryTests
{
    private readonly Mock<ICurrentUserService>        _currentUser = new();
    private readonly Mock<IFavoriteServiceRepository> _repo        = new();

    private GetCustomerFavoritesQueryHandler MakeHandler() =>
        new(_currentUser.Object, _repo.Object);

    [Fact]
    public async Task Handle_CustomerWithFavorites_ReturnsList()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)customerId);

        var fav = FavoriteService.Create(customerId, serviceId);
        _repo.Setup(r => r.GetByCustomerAsync(customerId, default))
            .ReturnsAsync(new List<FavoriteService> { fav });

        var result = await MakeHandler().Handle(new GetCustomerFavoritesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].ServiceId.Should().Be(serviceId);
    }
}
