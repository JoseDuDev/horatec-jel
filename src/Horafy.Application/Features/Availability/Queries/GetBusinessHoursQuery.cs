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
                    : new BusinessHoursResult(day, new TimeOnly(9, 0), new TimeOnly(18, 0), false);
            })
            .ToList();

        return Result.Success<IReadOnlyList<BusinessHoursResult>>(result);
    }
}
