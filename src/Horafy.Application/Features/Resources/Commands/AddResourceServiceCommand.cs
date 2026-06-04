using Horafy.Application.Features.Availability;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record AddResourceServiceCommand(Guid ResourceId, Guid ServiceId) : IRequest<Result>;

internal sealed class AddResourceServiceCommandHandler(
    IResourceRepository resourceRepository,
    IServiceRepository serviceRepository,
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<AddResourceServiceCommand, Result>
{
    public async Task<Result> Handle(
        AddResourceServiceCommand request, CancellationToken cancellationToken)
    {
        var resourceExists = await resourceRepository.ExistsAsync(
            r => r.Id == request.ResourceId, cancellationToken);
        if (!resourceExists)
            return Result.Failure(AvailabilityErrors.ResourceNotFound);

        var serviceExists = await serviceRepository.ExistsAsync(
            s => s.Id == request.ServiceId, cancellationToken);
        if (!serviceExists)
            return Result.Failure(AvailabilityErrors.ServiceNotFound);

        var alreadyLinked = await availabilityRepository.ResourceServiceExistsAsync(
            request.ResourceId, request.ServiceId, cancellationToken);
        if (alreadyLinked)
            return Result.Failure(AvailabilityErrors.ServiceAlreadyLinked);

        availabilityRepository.Add(ResourceService.Create(request.ResourceId, request.ServiceId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
