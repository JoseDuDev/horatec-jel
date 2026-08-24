using Horafy.Application.Features.Availability;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
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
    ITenantRepository tenantRepository,
    ICurrentTenantService currentTenant,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAvailableSlotsQuery, Result<IReadOnlyList<DateTimeOffset>>>
{
    public async Task<Result<IReadOnlyList<DateTimeOffset>>> Handle(
        GetAvailableSlotsQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId!.Value, cancellationToken);
        var tenantTimeZone = TimeZoneInfo.FindSystemTimeZoneById(tenant!.TimeZoneId);

        int? serviceDuration = null;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, cancellationToken);
            serviceDuration = service?.DurationMinutes;
        }

        // Recurso sem grade própria usa os horários globais do negócio.
        var rule = await availabilityRepository.GetRuleAsync(
            request.ResourceId, request.Date.DayOfWeek, cancellationToken);
        if (rule is null)
        {
            var businessHours = await availabilityRepository.GetBusinessHoursByDayAsync(
                request.Date.DayOfWeek, cancellationToken);
            if (businessHours is null || !businessHours.IsOpen)
                return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

            rule = AvailabilityRule.Create(
                request.ResourceId, request.Date.DayOfWeek,
                businessHours.OpenTime, businessHours.CloseTime,
                slotDurationMinutes: serviceDuration ?? 30);
        }

        if (await availabilityRepository.IsBlackoutAsync(request.Date, cancellationToken))
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        var exception = await availabilityRepository.GetExceptionAsync(
            request.ResourceId, request.Date, cancellationToken);
        if (exception?.IsBlocked is true)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        var dayStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
            request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), tenantTimeZone), TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
            request.Date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), tenantTimeZone), TimeSpan.Zero);
        var bookings = await bookingRepository.GetByResourceAsync(
            request.ResourceId, dayStart, dayEnd, cancellationToken);

        var slots = SlotCalculator.ComputeAvailableSlots(
            request.Date, rule, isBlackout: false, exception, serviceDuration,
            bookings, dateTimeProvider.UtcNow, tenantTimeZone);

        return Result.Success(slots);
    }
}
