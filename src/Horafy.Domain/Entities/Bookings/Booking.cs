using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Bookings;

namespace Horafy.Domain.Entities.Bookings;

public sealed class Booking : BaseEntity
{
    private Booking() { }

    public Guid ServiceId  { get; private set; }
    public Guid ResourceId { get; private set; }
    public Guid CustomerId { get; private set; }

    public string CustomerName  { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;

    public DateTimeOffset ScheduledAt     { get; private set; }
    public DateTimeOffset EndsAt          { get; private set; }
    public int            DurationMinutes { get; private set; }

    public string? Notes { get; private set; }

    public BookingStatus Status             { get; private set; } = BookingStatus.Pending;
    public string?       CancellationReason { get; private set; }

    public DateTimeOffset? ConfirmedAt  { get; private set; }
    public DateTimeOffset? CancelledAt  { get; private set; }
    public DateTimeOffset? CompletedAt  { get; private set; }

    public Guid?           RecurrenceGroupId { get; private set; }
    public DateTimeOffset? ExpiresAt         { get; private set; }

    public static Booking Create(
        Guid serviceId,
        Guid resourceId,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateTimeOffset scheduledAt,
        int durationMinutes,
        string? notes = null,
        Guid? recurrenceGroupId = null,
        DateTimeOffset? expiresAt = null)
    {
        if (scheduledAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("A data do agendamento deve ser futura.", nameof(scheduledAt));

        if (durationMinutes <= 0)
            throw new ArgumentException("Duração deve ser maior que zero.", nameof(durationMinutes));

        var booking = new Booking
        {
            ServiceId         = serviceId,
            ResourceId        = resourceId,
            CustomerId        = customerId,
            CustomerName      = customerName.Trim(),
            CustomerEmail     = customerEmail.ToLowerInvariant().Trim(),
            ScheduledAt       = scheduledAt,
            EndsAt            = scheduledAt.AddMinutes(durationMinutes),
            DurationMinutes   = durationMinutes,
            Notes             = notes?.Trim(),
            RecurrenceGroupId = recurrenceGroupId,
            ExpiresAt         = expiresAt
        };

        booking.RaiseDomainEvent(new BookingCreatedEvent(
            booking.Id, serviceId, resourceId, customerId, scheduledAt));

        return booking;
    }

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Não é possível confirmar um agendamento no status {Status}.");

        Status      = BookingStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        if (Status is BookingStatus.Completed or BookingStatus.Cancelled)
            throw new InvalidOperationException($"Não é possível cancelar um agendamento no status {Status}.");

        Status             = BookingStatus.Cancelled;
        CancellationReason = reason?.Trim();
        CancelledAt        = DateTimeOffset.UtcNow;
        UpdatedAt          = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new BookingCancelledEvent(Id, CustomerId, reason));
    }

    public void Complete()
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("Apenas agendamentos confirmados podem ser concluídos.");

        Status      = BookingStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }

    public void MarkNoShow()
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("Apenas agendamentos confirmados podem ser marcados como no-show.");

        Status    = BookingStatus.NoShow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool OverlapsWith(DateTimeOffset start, DateTimeOffset end) =>
        Status is not (BookingStatus.Cancelled or BookingStatus.NoShow)
        && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow)
        && ScheduledAt < end
        && EndsAt > start;
}
