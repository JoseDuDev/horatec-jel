using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Queries;

/// <summary>
/// Lista os profissionais (recursos) que atendem um determinado serviço.
/// Usado pelo bot do Atendefy: após o cliente escolher o serviço, oferece
/// apenas os profissionais habilitados para ele. Reusa <see cref="ResourceResult"/>.
/// </summary>
public sealed record GetResourcesByServiceQuery(
    Guid ServiceId,
    bool OnlyActive = true) : IRequest<Result<IReadOnlyList<ResourceResult>>>;

internal sealed class GetResourcesByServiceQueryHandler(
    IResourceRepository resourceRepository,
    IAvailabilityRepository availabilityRepository)
    : IRequestHandler<GetResourcesByServiceQuery, Result<IReadOnlyList<ResourceResult>>>
{
    public async Task<Result<IReadOnlyList<ResourceResult>>> Handle(
        GetResourcesByServiceQuery request, CancellationToken cancellationToken)
    {
        // 1. IDs dos recursos vinculados ao serviço
        var links = await availabilityRepository.GetResourcesByServiceAsync(
            request.ServiceId, cancellationToken);
        var resourceIds = links.Select(l => l.ResourceId).ToHashSet();

        if (resourceIds.Count == 0)
            return Result.Success<IReadOnlyList<ResourceResult>>(Array.Empty<ResourceResult>());

        // 2. Carrega os recursos (ativos por padrão) e filtra pelos vinculados ao serviço
        var resources = request.OnlyActive
            ? await resourceRepository.GetActiveAsync(cancellationToken)
            : await resourceRepository.GetAllAsync(cancellationToken);

        var filtered = resources.Where(r => resourceIds.Contains(r.Id)).ToList();

        // 3. Para cada recurso, lista todos os serviços que ele atende (igual ao GetResourcesQuery)
        var services = await availabilityRepository.GetServicesByResourcesAsync(
            filtered.Select(r => r.Id), cancellationToken);
        var servicesByResource = services.ToLookup(rs => rs.ResourceId, rs => rs.ServiceId);

        var result = filtered
            .Select(r => new ResourceResult(
                r.Id, r.Name, r.Type, r.Email, r.Phone, r.Specialty, r.Bio, r.AvatarUrl,
                r.IsActive, servicesByResource[r.Id].ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<ResourceResult>>(result);
    }
}
