using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetResourceExceptionsQuery(Guid ResourceId, DateOnly From, DateOnly To)
    : IRequest<Result<IReadOnlyList<AvailabilityExceptionResult>>>;

public sealed record AvailabilityExceptionResult(
    Guid Id,
    Guid ResourceId,
    DateOnly Date,
    bool IsBlocked,
    TimeOnly? CustomStart,
    TimeOnly? CustomEnd,
    string? Reason);

internal sealed class GetResourceExceptionsQueryHandler(
    IAvailabilityRepository repository)
    : IRequestHandler<GetResourceExceptionsQuery, Result<IReadOnlyList<AvailabilityExceptionResult>>>
{
    public async Task<Result<IReadOnlyList<AvailabilityExceptionResult>>> Handle(
        GetResourceExceptionsQuery request, CancellationToken cancellationToken)
    {
        var exceptions = await repository.GetExceptionsByResourceAsync(
            request.ResourceId, request.From, request.To, cancellationToken);

        var result = exceptions.Select(e => new AvailabilityExceptionResult(
            e.Id, e.ResourceId, e.Date,
            e.IsBlocked, e.CustomStart, e.CustomEnd, e.Reason)).ToList();

        return Result.Success<IReadOnlyList<AvailabilityExceptionResult>>(result);
    }
}
