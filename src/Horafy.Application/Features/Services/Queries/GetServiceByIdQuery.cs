using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Services.Queries;

public sealed record GetServiceByIdQuery(Guid Id) : IRequest<Result<ServiceResult>>;

internal sealed class GetServiceByIdQueryHandler(
    IServiceRepository serviceRepository) : IRequestHandler<GetServiceByIdQuery, Result<ServiceResult>>
{
    public async Task<Result<ServiceResult>> Handle(
        GetServiceByIdQuery request, CancellationToken cancellationToken)
    {
        var service = await serviceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (service is null) return Result.Failure<ServiceResult>(ServiceErrors.NotFound);

        return Result.Success(new ServiceResult(
            service.Id, service.Name, service.Description,
            service.DurationMinutes, service.Price, service.Category, service.IsActive));
    }
}
