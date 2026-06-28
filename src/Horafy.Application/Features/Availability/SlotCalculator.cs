using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Bookings;

namespace Horafy.Application.Features.Availability;

public static class SlotCalculator
{
    public static IReadOnlyList<DateTimeOffset> ComputeAvailableSlots(
        DateOnly                date,
        AvailabilityRule?       rule,
        bool                    isBlackout,
        AvailabilityException?  exception,
        int?                    serviceDurationMinutes,
        IReadOnlyList<Booking>  dayBookings,
        DateTimeOffset          now)
    {
        if (rule is null) return Array.Empty<DateTimeOffset>();
        if (isBlackout) return Array.Empty<DateTimeOffset>();
        if (exception?.IsBlocked is true) return Array.Empty<DateTimeOffset>();

        var windowStart = exception?.CustomStart ?? rule.StartTime;
        var windowEnd   = exception?.CustomEnd   ?? rule.EndTime;

        var slotDuration = serviceDurationMinutes ?? rule.SlotDurationMinutes;
        var step         = slotDuration + rule.BreakAfterMinutes;

        var allSlots = new List<DateTimeOffset>();
        var current  = windowStart;
        while (current.Add(TimeSpan.FromMinutes(slotDuration)) <= windowEnd)
        {
            allSlots.Add(new DateTimeOffset(date.ToDateTime(current, DateTimeKind.Utc)));
            current = current.Add(TimeSpan.FromMinutes(step));
        }
        if (allSlots.Count == 0) return Array.Empty<DateTimeOffset>();

        return allSlots
            .Where(slot => slot > now)
            .Where(slot => !dayBookings.Any(b =>
                b.OverlapsWith(slot, slot.AddMinutes(slotDuration))))
            .ToList();
    }
}
