using FluentAssertions;
using Horafy.Domain.Entities.Favorites;
using Xunit;

namespace Horafy.Domain.Tests.Favorites;

public sealed class FavoriteServiceTests
{
    [Fact]
    public void Create_ValidData_SetsProperties()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();

        var fav = FavoriteService.Create(customerId, serviceId);

        fav.CustomerId.Should().Be(customerId);
        fav.ServiceId.Should().Be(serviceId);
        fav.Id.Should().NotBeEmpty();
    }
}
