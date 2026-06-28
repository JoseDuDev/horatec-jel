using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetBlackoutDatesQuery(int? Year = null)
    : IRequest<Result<IReadOnlyList<BlackoutDateResult>>>;

public sealed record BlackoutDateResult(
    Guid     Id,
    DateOnly Date,
    string?  Reason);

internal sealed class GetBlackoutDatesQueryHandler(IAvailabilityRepository availabilityRepository)
    : IRequestHandler<GetBlackoutDatesQuery, Result<IReadOnlyList<BlackoutDateResult>>>
{
    public async Task<Result<IReadOnlyList<BlackoutDateResult>>> Handle(
        GetBlackoutDatesQuery request, CancellationToken cancellationToken)
    {
        var blackouts = await availabilityRepository.GetBlackoutDatesAsync(request.Year, cancellationToken);

        IReadOnlyList<BlackoutDateResult> results = blackouts
            .Select(b => new BlackoutDateResult(b.Id, b.Date, b.Reason))
            .ToList();

        return Result<IReadOnlyList<BlackoutDateResult>>.Success(results);
    }
}
