using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Resources;

public sealed class ResourceService : BaseEntity
{
    private ResourceService() { }

    public Guid ResourceId { get; private set; }
    public Guid ServiceId  { get; private set; }

    public static ResourceService Create(Guid resourceId, Guid serviceId) =>
        new() { ResourceId = resourceId, ServiceId = serviceId };
}
