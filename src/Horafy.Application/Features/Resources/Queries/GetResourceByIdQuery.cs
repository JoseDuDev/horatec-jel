using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Queries;

public sealed record GetResourceByIdQuery(Guid Id) : IRequest<Result<ResourceResult>>;

internal sealed class GetResourceByIdQueryHandler(
    IResourceRepository resourceRepository,
    IAvailabilityRepository availabilityRepository)
    : IRequestHandler<GetResourceByIdQuery, Result<ResourceResult>>
{
    public async Task<Result<ResourceResult>> Handle(
        GetResourceByIdQuery request, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (resource is null) return Result.Failure<ResourceResult>(ResourceErrors.NotFound);

        var services = await availabilityRepository.GetResourceServicesAsync(resource.Id, cancellationToken);
        var serviceIds = services.Select(rs => rs.ServiceId).ToList();

        return Result.Success(new ResourceResult(
            resource.Id, resource.Name, resource.Type, resource.Email,
            resource.Phone, resource.Specialty, resource.Bio,
            resource.AvatarUrl, resource.IsActive, serviceIds));
    }
}
