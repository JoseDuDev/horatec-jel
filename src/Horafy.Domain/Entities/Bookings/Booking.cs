using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Bookings;

namespace Horafy.Domain.Entities.Bookings;

public sealed class Booking : BaseEntity
{
    private Booking() { }
    private readonly List<BookingService> _services = [];

    public Guid ServiceId  { get; private set; }
    public Guid ResourceId { get; private set; }
    public Guid CustomerId { get; private set; }

    public string CustomerName  { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;
    public string? CustomerPhone { get; private set; }

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

    public BookingPaymentStatus PaymentStatus { get; private set; } = BookingPaymentStatus.NotRequired;

    public IReadOnlyList<BookingService> Services => _services.AsReadOnly();

    public static Booking Create(
        IReadOnlyList<(Guid ServiceId, string ServiceName, int DurationMinutes)> services,
        Guid resourceId,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateTimeOffset scheduledAt,
        string? customerPhone = null,
        string? notes = null,
        Guid? recurrenceGroupId = null,
        DateTimeOffset? expiresAt = null)
    {
        if (services.Count == 0)
            throw new ArgumentException("Pelo menos um serviço é obrigatório.", nameof(services));

        if (scheduledAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("A data do agendamento deve ser futura.", nameof(scheduledAt));

        var totalDuration = services.Sum(s => s.DurationMinutes);

        if (totalDuration <= 0)
            throw new ArgumentException("Duração total deve ser maior que zero.", nameof(services));

        var booking = new Booking
        {
            ServiceId         = services[0].ServiceId,
            ResourceId        = resourceId,
            CustomerId        = customerId,
            CustomerName      = customerName.Trim(),
            CustomerEmail     = customerEmail.ToLowerInvariant().Trim(),
            CustomerPhone     = customerPhone?.Trim(),
            ScheduledAt       = scheduledAt,
            EndsAt            = scheduledAt.AddMinutes(totalDuration),
            DurationMinutes   = totalDuration,
            Notes             = notes?.Trim(),
            RecurrenceGroupId = recurrenceGroupId,
            ExpiresAt         = expiresAt
        };

        foreach (var svc in services)
            booking._services.Add(BookingService.Create(booking.Id, svc.ServiceId, svc.ServiceName, svc.DurationMinutes));

        booking.RaiseDomainEvent(new BookingCreatedEvent(
            booking.Id, booking.ServiceId, resourceId, customerId, booking.CustomerPhone, scheduledAt));

        return booking;
    }

    public static Booking Create(
        Guid serviceId,
        Guid resourceId,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateTimeOffset scheduledAt,
        int durationMinutes,
        string? customerPhone = null,
        string? notes = null,
        Guid? recurrenceGroupId = null,
        DateTimeOffset? expiresAt = null) =>
        Create(
            new[] { (serviceId, ServiceName: serviceId.ToString(), durationMinutes) },
            resourceId, customerId, customerName, customerEmail,
            scheduledAt, customerPhone, notes, recurrenceGroupId, expiresAt);

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Não é possível confirmar um agendamento no status {Status}.");

        Status      = BookingStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new BookingConfirmedEvent(Id, CustomerId, CustomerName, CustomerEmail, CustomerPhone, ScheduledAt));
    }

    public void Cancel(string? reason = null)
    {
        if (Status is BookingStatus.Completed or BookingStatus.Cancelled)
            throw new InvalidOperationException($"Não é possível cancelar um agendamento no status {Status}.");

        Status             = BookingStatus.Cancelled;
        CancellationReason = reason?.Trim();
        CancelledAt        = DateTimeOffset.UtcNow;
        UpdatedAt          = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new BookingCancelledEvent(Id, CustomerId, CustomerPhone, reason));
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

    public void MarkPaymentPending()  => PaymentStatus = BookingPaymentStatus.Pending;
    public void MarkPaymentPaid()     { PaymentStatus = BookingPaymentStatus.Paid;          UpdatedAt = DateTimeOffset.UtcNow; }
    public void MarkPaymentPartial()  { PaymentStatus = BookingPaymentStatus.PartiallyPaid; UpdatedAt = DateTimeOffset.UtcNow; }
    public void MarkPaymentRefunded() { PaymentStatus = BookingPaymentStatus.Refunded;      UpdatedAt = DateTimeOffset.UtcNow; }

    public bool OverlapsWith(DateTimeOffset start, DateTimeOffset end) =>
        Status is not (BookingStatus.Cancelled or BookingStatus.NoShow)
        && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow)
        && ScheduledAt < end
        && EndsAt > start;
}
