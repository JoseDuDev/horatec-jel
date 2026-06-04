using Horafy.Application.Features.Availability;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record RemoveResourceServiceCommand(Guid ResourceId, Guid ServiceId) : IRequest<Result>;

internal sealed class RemoveResourceServiceCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<RemoveResourceServiceCommand, Result>
{
    public async Task<Result> Handle(
        RemoveResourceServiceCommand request, CancellationToken cancellationToken)
    {
        var links = await availabilityRepository
            .GetResourceServicesAsync(request.ResourceId, cancellationToken);

        var link = links.FirstOrDefault(rs => rs.ServiceId == request.ServiceId);
        if (link is null)
            return Result.Failure(AvailabilityErrors.ServiceNotLinked);

        availabilityRepository.Remove(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
