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
        // 1. Regra semanal do recurso
        var rule = await availabilityRepository.GetRuleAsync(
            request.ResourceId, request.Date.DayOfWeek, cancellationToken);

        if (rule is null)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        // 2. Bloqueio global do tenant (fecha todos os recursos na data)
        if (await availabilityRepository.IsBlackoutAsync(request.Date, cancellationToken))
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        // 3. Verificar exceção para a data
        var exception = await availabilityRepository.GetExceptionAsync(
            request.ResourceId, request.Date, cancellationToken);

        if (exception?.IsBlocked is true)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        var windowStart = exception?.CustomStart ?? rule.StartTime;
        var windowEnd   = exception?.CustomEnd   ?? rule.EndTime;

        // 3. Duração do slot: usa serviço se informado, senão usa a duração da regra
        int slotDuration = rule.SlotDurationMinutes;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, cancellationToken);
            if (service is not null)
                slotDuration = service.DurationMinutes;
        }

        // 4. Gerar todos os slots possíveis na janela
        var step     = slotDuration + rule.BreakAfterMinutes;
        var allSlots = new List<DateTimeOffset>();
        var current  = windowStart;

        while (current.Add(TimeSpan.FromMinutes(slotDuration)) <= windowEnd)
        {
            var slotStart = new DateTimeOffset(
                request.Date.ToDateTime(current, DateTimeKind.Utc));
            allSlots.Add(slotStart);
            current = current.Add(TimeSpan.FromMinutes(step));
        }

        if (allSlots.Count == 0)
            return Result.Success<IReadOnlyList<DateTimeOffset>>(Array.Empty<DateTimeOffset>());

        // 5. Buscar bookings existentes na janela do dia
        var dayStart = new DateTimeOffset(request.Date.ToDateTime(windowStart, DateTimeKind.Utc));
        var dayEnd   = new DateTimeOffset(request.Date.ToDateTime(windowEnd,   DateTimeKind.Utc));

        var existingBookings = await bookingRepository.GetByResourceAsync(
            request.ResourceId, dayStart, dayEnd, cancellationToken);

        // 6. Filtrar slots no passado (não faz sentido oferecer horário já vencido)
        var now = dateTimeProvider.UtcNow;

        // 7. Filtrar slots ocupados e passados
        var availableSlots = allSlots
            .Where(slot => slot > now)
            .Where(slot => !existingBookings.Any(b =>
                b.OverlapsWith(slot, slot.AddMinutes(slotDuration))))
            .ToList();

        return Result.Success<IReadOnlyList<DateTimeOffset>>(availableSlots);
    }
}
