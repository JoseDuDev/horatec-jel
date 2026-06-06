using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Favorites;

public sealed class FavoriteService : BaseEntity
{
    private FavoriteService() { }

    public Guid CustomerId { get; private set; }
    public Guid ServiceId  { get; private set; }

    public static FavoriteService Create(Guid customerId, Guid serviceId) =>
        new()
        {
            CustomerId = customerId,
            ServiceId  = serviceId
        };
}
