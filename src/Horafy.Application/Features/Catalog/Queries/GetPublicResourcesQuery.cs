using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Catalog.Queries;

public sealed record GetPublicResourcesQuery : IRequest<Result<IReadOnlyList<PublicResourceResult>>>;

public sealed record PublicResourceResult(
    Guid    Id,
    string  Name,
    string? Specialty,
    string? Bio,
    string? AvatarUrl);

internal sealed class GetPublicResourcesQueryHandler(IResourceRepository resourceRepository)
    : IRequestHandler<GetPublicResourcesQuery, Result<IReadOnlyList<PublicResourceResult>>>
{
    public async Task<Result<IReadOnlyList<PublicResourceResult>>> Handle(
        GetPublicResourcesQuery request, CancellationToken ct)
    {
        var resources = await resourceRepository.GetActiveAsync(ct);

        IReadOnlyList<PublicResourceResult> results = resources
            .Select(r => new PublicResourceResult(
                r.Id, r.Name, r.Specialty, r.Bio, r.AvatarUrl))
            .ToList();

        return Result<IReadOnlyList<PublicResourceResult>>.Success(results);
    }
}
