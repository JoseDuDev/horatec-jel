using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
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

internal sealed class GetAvailabilityCalendarQueryHandler(
    IAvailabilityRepository availabilityRepository,
    IServiceRepository serviceRepository,
    IBookingRepository bookingRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAvailabilityCalendarQuery, Result<IReadOnlyList<DayAvailability>>>
{
    public async Task<Result<IReadOnlyList<DayAvailability>>> Handle(
        GetAvailabilityCalendarQuery request, CancellationToken ct)
    {
        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var from = new DateOnly(request.Year, request.Month, 1);
        var to   = new DateOnly(request.Year, request.Month, daysInMonth);

        var rules = (await availabilityRepository.GetRulesByResourceAsync(request.ResourceId, ct))
            .GroupBy(r => r.DayOfWeek).ToDictionary(g => g.Key, g => g.First());
        var exceptions = (await availabilityRepository.GetExceptionsByResourceAsync(request.ResourceId, from, to, ct))
            .GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.First());
        var blackouts = (await availabilityRepository.GetBlackoutDatesAsync(request.Year, ct))
            .Select(b => b.Date).ToHashSet();

        int? serviceDuration = null;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, ct);
            serviceDuration = service?.DurationMinutes;
        }

        var monthStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var monthEnd   = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var bookingsByDate = (await bookingRepository.GetByResourceAsync(request.ResourceId, monthStart, monthEnd, ct))
            .GroupBy(b => DateOnly.FromDateTime(b.ScheduledAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Booking>)g.ToList());

        var now    = dateTimeProvider.UtcNow;
        var result = new List<DayAvailability>(daysInMonth);
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(request.Year, request.Month, day);
            rules.TryGetValue(date.DayOfWeek, out var rule);
            exceptions.TryGetValue(date, out var exception);
            bookingsByDate.TryGetValue(date, out var dayBookings);

            var slots = SlotCalculator.ComputeAvailableSlots(
                date, rule, blackouts.Contains(date), exception, serviceDuration,
                dayBookings ?? Array.Empty<Booking>(), now);

            result.Add(new DayAvailability(date, slots.Count));
        }

        return Result.Success<IReadOnlyList<DayAvailability>>(result);
    }
}
