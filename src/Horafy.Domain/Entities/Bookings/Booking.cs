using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Bookings;

namespace Horafy.Domain.Entities.Bookings;

public sealed class Booking : BaseEntity
{
    private Booking() { }
    private readonly List<BookingService> _services = [];

    // Nulos em reservas de locação (Kind = Rental), que não têm serviço nem recurso.
    public Guid? ServiceId   { get; private set; }
    public Guid? ResourceId  { get; private set; }
    public string ResourceName { get; private set; } = default!;
    public Guid CustomerId   { get; private set; }

    /// <summary>Modo da reserva: agendamento por horário (default) ou locação multi-dia.</summary>
    public BookingKind Kind { get; private set; } = BookingKind.Appointment;

    /// <summary>Estágio do ciclo de vida da locação. Null em agendamentos.</summary>
    public RentalLifecycle? RentalStatus { get; private set; }

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

    /// <summary>Valor total = soma dos preços (snapshot) dos serviços/diárias.</summary>
    public decimal TotalAmount => _services.Sum(s => s.Price);

    /// <summary>Caução (snapshot) retida em locações; reembolsável na devolução. 0 em agendamentos.</summary>
    public decimal SecurityDeposit { get; private set; }

    /// <summary>Valor a cobrar no checkout = diárias/serviços + caução.</summary>
    public decimal PayableAmount => TotalAmount + SecurityDeposit;

    public static Booking Create(
        IReadOnlyList<(Guid ServiceId, string ServiceName, int DurationMinutes, decimal Price)> services,
        Guid resourceId,
        string resourceName,
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
            ResourceName      = resourceName.Trim(),
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
            booking._services.Add(BookingService.Create(booking.Id, svc.ServiceId, svc.ServiceName, svc.DurationMinutes, svc.Price));

        booking.RaiseDomainEvent(new BookingCreatedEvent(
            booking.Id, services[0].ServiceId, resourceId, customerId, booking.CustomerPhone, scheduledAt));

        return booking;
    }

    /// <summary>
    /// Cria uma reserva de locação (Kind = Rental). Sem serviço/recurso; o período é
    /// [<paramref name="startsAt"/> (retirada), <paramref name="endsAt"/> (devolução)].
    /// Cada item vira uma linha (<see cref="BookingService"/>) com o item de locação,
    /// as unidades e o snapshot do valor das diárias. Não dispara eventos de notificação
    /// (locação ainda não tem fluxo de notificação — ver docs/rental-plan.md, Fase 6).
    /// </summary>
    public static Booking CreateRental(
        IReadOnlyList<(Guid RentableItemId, string ItemName, int Quantity, decimal LineTotal)> items,
        Guid customerId,
        string customerName,
        string customerEmail,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        decimal securityDeposit = 0,
        string? customerPhone = null,
        string? notes = null)
    {
        if (items.Count == 0)
            throw new ArgumentException("Pelo menos um item é obrigatório.", nameof(items));

        if (endsAt <= startsAt)
            throw new ArgumentException("A devolução deve ser posterior à retirada.", nameof(endsAt));

        if (securityDeposit < 0)
            throw new ArgumentException("Caução não pode ser negativa.", nameof(securityDeposit));

        var booking = new Booking
        {
            ServiceId       = null,
            ResourceId      = null,
            ResourceName    = string.Empty,
            CustomerId      = customerId,
            CustomerName    = customerName.Trim(),
            CustomerEmail   = customerEmail.ToLowerInvariant().Trim(),
            CustomerPhone   = customerPhone?.Trim(),
            Kind            = BookingKind.Rental,
            RentalStatus    = RentalLifecycle.Reserved,
            SecurityDeposit = securityDeposit,
            ScheduledAt     = startsAt,
            EndsAt          = endsAt,
            DurationMinutes = (int)(endsAt - startsAt).TotalMinutes,
            Notes           = notes?.Trim(),
        };

        foreach (var it in items)
            booking._services.Add(BookingService.Create(
                booking.Id,
                serviceId:      Guid.Empty,
                serviceName:    it.ItemName,
                durationMinutes: 0,
                price:          it.LineTotal,
                rentableItemId: it.RentableItemId,
                quantity:       it.Quantity));

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
            new[] { (serviceId, ServiceName: serviceId.ToString(), durationMinutes, Price: 0m) },
            resourceId, resourceName: string.Empty, customerId, customerName, customerEmail,
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
        RaiseDomainEvent(new BookingCompletedEvent(Id, CustomerId));
    }

    public void MarkNoShow()
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("Apenas agendamentos confirmados podem ser marcados como no-show.");

        Status    = BookingStatus.NoShow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // ── Ciclo de vida da locação ───────────────────────────────────────────────

    /// <summary>Marca a retirada do item (Reserved → PickedUp). Exige reserva confirmada.</summary>
    public void MarkRentalPickedUp()
    {
        if (Kind != BookingKind.Rental)
            throw new InvalidOperationException("Apenas locações podem ser retiradas.");
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("A locação precisa estar confirmada para a retirada.");
        if (RentalStatus != RentalLifecycle.Reserved)
            throw new InvalidOperationException($"Não é possível retirar uma locação no estágio {RentalStatus}.");

        RentalStatus = RentalLifecycle.PickedUp;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marca a devolução do item (PickedUp → Returned), encerra a reserva e libera o
    /// estoque. Não dispara <see cref="BookingCompletedEvent"/> — locação não gera fidelidade.
    /// </summary>
    public void MarkRentalReturned(DateTimeOffset returnedAt)
    {
        if (Kind != BookingKind.Rental)
            throw new InvalidOperationException("Apenas locações podem ser devolvidas.");
        if (RentalStatus != RentalLifecycle.PickedUp)
            throw new InvalidOperationException($"Não é possível devolver uma locação no estágio {RentalStatus}.");

        RentalStatus = RentalLifecycle.Returned;
        Status       = BookingStatus.Completed;
        CompletedAt  = returnedAt;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }

    /// <summary>Locação em atraso: item retirado e ainda não devolvido após a data prevista.</summary>
    public bool IsOverdue(DateTimeOffset now) =>
        Kind == BookingKind.Rental
        && RentalStatus == RentalLifecycle.PickedUp
        && now > EndsAt;

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
