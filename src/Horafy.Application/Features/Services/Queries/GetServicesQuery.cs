using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Services.Queries;

public sealed record GetServicesQuery(bool OnlyActive = true) : IRequest<Result<IReadOnlyList<ServiceResult>>>;

public sealed record ServiceResult(
    Guid Id,
    string Name,
    string? Description,
    int DurationMinutes,
    decimal Price,
    string? Category,
    bool IsActive);

internal sealed class GetServicesQueryHandler(
    IServiceRepository serviceRepository) : IRequestHandler<GetServicesQuery, Result<IReadOnlyList<ServiceResult>>>
{
    public async Task<Result<IReadOnlyList<ServiceResult>>> Handle(
        GetServicesQuery request, CancellationToken cancellationToken)
    {
        var services = request.OnlyActive
            ? await serviceRepository.GetActiveAsync(cancellationToken)
            : await serviceRepository.GetAllAsync(cancellationToken);

        var result = services
            .Select(s => new ServiceResult(s.Id, s.Name, s.Description,
                s.DurationMinutes, s.Price, s.Category, s.IsActive))
            .ToList();

        return Result.Success<IReadOnlyList<ServiceResult>>(result);
    }
}
