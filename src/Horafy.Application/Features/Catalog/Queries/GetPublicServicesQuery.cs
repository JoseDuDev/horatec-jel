using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Catalog.Queries;

public sealed record GetPublicServicesQuery : IRequest<Result<IReadOnlyList<PublicServiceResult>>>;

public sealed record PublicServiceResult(
    Guid    Id,
    string  Name,
    string? Description,
    int     DurationMinutes,
    decimal Price,
    string? Category);

internal sealed class GetPublicServicesQueryHandler(IServiceRepository serviceRepository)
    : IRequestHandler<GetPublicServicesQuery, Result<IReadOnlyList<PublicServiceResult>>>
{
    public async Task<Result<IReadOnlyList<PublicServiceResult>>> Handle(
        GetPublicServicesQuery request, CancellationToken ct)
    {
        var services = await serviceRepository.GetActiveAsync(ct);

        IReadOnlyList<PublicServiceResult> results = services
            .Select(s => new PublicServiceResult(
                s.Id, s.Name, s.Description, s.DurationMinutes, s.Price, s.Category))
            .ToList();

        return Result<IReadOnlyList<PublicServiceResult>>.Success(results);
    }
}
