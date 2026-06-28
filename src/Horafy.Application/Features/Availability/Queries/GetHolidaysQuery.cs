using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetHolidaysQuery(int? Year = null)
    : IRequest<Result<IReadOnlyList<HolidayResult>>>;

public sealed record HolidayResult(
    Guid    Id,
    string  Name,
    DateOnly Date,
    bool    IsRecurringAnnually,
    string? Reason);

internal sealed class GetHolidaysQueryHandler(IAvailabilityRepository availabilityRepository)
    : IRequestHandler<GetHolidaysQuery, Result<IReadOnlyList<HolidayResult>>>
{
    public async Task<Result<IReadOnlyList<HolidayResult>>> Handle(
        GetHolidaysQuery request, CancellationToken cancellationToken)
    {
        var holidays = await availabilityRepository
            .GetHolidaysAsync(request.Year, cancellationToken);

        IReadOnlyList<HolidayResult> results = holidays
            .Select(h => new HolidayResult(
                h.Id, h.Name, h.Date, h.IsRecurringAnnually, h.Reason))
            .ToList();

        return Result<IReadOnlyList<HolidayResult>>.Success(results);
    }
}
