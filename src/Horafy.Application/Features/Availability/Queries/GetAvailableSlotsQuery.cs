using Horafy.Application.Features.Availability;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetAvailableSlotsQuery(
    Guid ResourceId,
    DateOnly Date,
    Guid? ServiceId) : IRequest<Result<IReadOnlyList<DateTimeOffset>>>;

internal sealed class GetAvailableSlotsQueryHandler(
    IAvailabilityRepository availabilityRepository,
    IServiceRepository serviceRepository,
    IBookingRepository bookingRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAvailableSlotsQuery, Result<IReadOnlyList<DateTimeOffset>>>
{
    public async Task<Result<IReadOnlyList<DateTimeOffset>>> Handle(
        GetAvailableSlotsQuery request, CancellationToken cancellationToken)
    {
        var rule = await availabilityRepository.GetRuleAsync(
            request.ResourceId, request.Date.DayOfWeek, cancellationToken);
        if (rule is null)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        if (await availabilityRepository.IsBlackoutAsync(request.Date, cancellationToken))
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        var exception = await availabilityRepository.GetExceptionAsync(
            request.ResourceId, request.Date, cancellationToken);
        if (exception?.IsBlocked is true)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        int? serviceDuration = null;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, cancellationToken);
            serviceDuration = service?.DurationMinutes;
        }

        var dayStart = new DateTimeOffset(request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var dayEnd   = new DateTimeOffset(request.Date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var bookings = await bookingRepository.GetByResourceAsync(
            request.ResourceId, dayStart, dayEnd, cancellationToken);

        var slots = SlotCalculator.ComputeAvailableSlots(
            request.Date, rule, isBlackout: false, exception, serviceDuration,
            bookings, dateTimeProvider.UtcNow);

        return Result.Success(slots);
    }
}
