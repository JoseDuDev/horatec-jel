using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Queries;

public sealed record GetResourcesQuery(
    bool OnlyActive = true,
    ResourceType? Type = null) : IRequest<Result<IReadOnlyList<ResourceResult>>>;

public sealed record ResourceResult(
    Guid Id,
    string Name,
    ResourceType Type,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    string? AvatarUrl,
    bool IsActive,
    IReadOnlyList<Guid> ServiceIds);

internal sealed class GetResourcesQueryHandler(
    IResourceRepository resourceRepository,
    IAvailabilityRepository availabilityRepository)
    : IRequestHandler<GetResourcesQuery, Result<IReadOnlyList<ResourceResult>>>
{
    public async Task<Result<IReadOnlyList<ResourceResult>>> Handle(
        GetResourcesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Resource> resources = request.Type.HasValue
            ? await resourceRepository.GetByTypeAsync(request.Type.Value, cancellationToken)
            : request.OnlyActive
                ? await resourceRepository.GetActiveAsync(cancellationToken)
                : await resourceRepository.GetAllAsync(cancellationToken);

        var resourceIds = resources.Select(r => r.Id).ToList();
        var services = await availabilityRepository.GetServicesByResourcesAsync(resourceIds, cancellationToken);
        var servicesByResource = services.ToLookup(rs => rs.ResourceId, rs => rs.ServiceId);

        var result = resources
            .Select(r => ToResult(r, servicesByResource[r.Id].ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<ResourceResult>>(result);
    }

    private static ResourceResult ToResult(Resource r, IReadOnlyList<Guid> serviceIds) => new(
        r.Id, r.Name, r.Type, r.Email, r.Phone, r.Specialty, r.Bio, r.AvatarUrl, r.IsActive, serviceIds);
}
