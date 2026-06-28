using FluentValidation;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record DayAvailability(DateOnly Date, int AvailableSlotCount);

public sealed record GetAvailabilityCalendarQuery(
    Guid ResourceId,
    int Year,
    int Month,
    Guid? ServiceId = null) : IRequest<Result<IReadOnlyList<DayAvailability>>>;

public sealed class GetAvailabilityCalendarQueryValidator : AbstractValidator<GetAvailabilityCalendarQuery>
{
    public GetAvailabilityCalendarQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2020, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

internal sealed class GetAvailabilityCalendarQueryHandler(ISender sender)
    : IRequestHandler<GetAvailabilityCalendarQuery, Result<IReadOnlyList<DayAvailability>>>
{
    public async Task<Result<IReadOnlyList<DayAvailability>>> Handle(
        GetAvailabilityCalendarQuery request, CancellationToken ct)
    {
        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var result      = new List<DayAvailability>(daysInMonth);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date  = new DateOnly(request.Year, request.Month, day);
            var slots = await sender.Send(
                new GetAvailableSlotsQuery(request.ResourceId, date, request.ServiceId), ct);
            result.Add(new DayAvailability(date, slots.IsSuccess ? slots.Value.Count : 0));
        }

        return Result.Success<IReadOnlyList<DayAvailability>>(result);
    }
}
