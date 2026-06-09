using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetBusinessHoursQuery : IRequest<Result<IReadOnlyList<BusinessHoursResult>>>;

public sealed record BusinessHoursResult(
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    bool IsOpen);

internal sealed class GetBusinessHoursQueryHandler(
    IAvailabilityRepository repository)
    : IRequestHandler<GetBusinessHoursQuery, Result<IReadOnlyList<BusinessHoursResult>>>
{
    private static readonly TimeOnly DefaultOpenTime  = new(9, 0);
    private static readonly TimeOnly DefaultCloseTime = new(18, 0);

    public async Task<Result<IReadOnlyList<BusinessHoursResult>>> Handle(
        GetBusinessHoursQuery request, CancellationToken cancellationToken)
    {
        var stored = await repository.GetBusinessHoursAsync(cancellationToken);

        var result = Enum.GetValues<DayOfWeek>()
            .Select(day =>
            {
                var bh = stored.FirstOrDefault(b => b.DayOfWeek == day);
                return bh is not null
                    ? new BusinessHoursResult(bh.DayOfWeek, bh.OpenTime, bh.CloseTime, bh.IsOpen)
                    : new BusinessHoursResult(day, DefaultOpenTime, DefaultCloseTime, false);
            })
            .ToList();

        return Result.Success<IReadOnlyList<BusinessHoursResult>>(result);
    }
}
