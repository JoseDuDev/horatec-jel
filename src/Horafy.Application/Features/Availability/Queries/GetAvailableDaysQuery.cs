using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

/// <summary>
/// Retorna os dias (no intervalo [From, To]) que têm ao menos um horário livre para o
/// recurso/serviço. Evita o cliente (bot) varrer dia a dia. Reusa a lógica de slots.
/// </summary>
public sealed record GetAvailableDaysQuery(
    Guid ResourceId,
    DateOnly From,
    DateOnly To,
    Guid? ServiceId) : IRequest<Result<IReadOnlyList<DateOnly>>>;

public sealed class GetAvailableDaysQueryValidator : AbstractValidator<GetAvailableDaysQuery>
{
    public const int MaxRangeDays = 31;

    public GetAvailableDaysQueryValidator()
    {
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("'to' deve ser maior ou igual a 'from'.");
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber <= MaxRangeDays)
            .WithMessage($"O intervalo não pode exceder {MaxRangeDays} dias.");
    }
}

internal sealed class GetAvailableDaysQueryHandler(
    IAvailabilityRepository availabilityRepository,
    IServiceRepository serviceRepository,
    IBookingRepository bookingRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAvailableDaysQuery, Result<IReadOnlyList<DateOnly>>>
{
    public async Task<Result<IReadOnlyList<DateOnly>>> Handle(
        GetAvailableDaysQuery request, CancellationToken ct)
    {
        var rules = (await availabilityRepository.GetRulesByResourceAsync(request.ResourceId, ct))
            .GroupBy(r => r.DayOfWeek).ToDictionary(g => g.Key, g => g.First());
        var exceptions = (await availabilityRepository.GetExceptionsByResourceAsync(request.ResourceId, request.From, request.To, ct))
            .GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.First());

        var blackouts = new HashSet<DateOnly>();
        for (var year = request.From.Year; year <= request.To.Year; year++)
            foreach (var b in await availabilityRepository.GetBlackoutDatesAsync(year, ct))
                blackouts.Add(b.Date);

        int? serviceDuration = null;
        if (request.ServiceId.HasValue)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId.Value, ct);
            serviceDuration = service?.DurationMinutes;
        }

        var rangeStart = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var rangeEnd   = new DateTimeOffset(request.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var bookingsByDate = (await bookingRepository.GetByResourceAsync(request.ResourceId, rangeStart, rangeEnd, ct))
            .GroupBy(b => DateOnly.FromDateTime(b.ScheduledAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Booking>)g.ToList());

        var now  = dateTimeProvider.UtcNow;
        var days = new List<DateOnly>();
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            rules.TryGetValue(date.DayOfWeek, out var rule);
            exceptions.TryGetValue(date, out var exception);
            bookingsByDate.TryGetValue(date, out var dayBookings);

            var slots = SlotCalculator.ComputeAvailableSlots(
                date, rule, blackouts.Contains(date), exception, serviceDuration,
                dayBookings ?? Array.Empty<Booking>(), now);

            if (slots.Count > 0) days.Add(date);
        }

        return Result.Success<IReadOnlyList<DateOnly>>(days);
    }
}
