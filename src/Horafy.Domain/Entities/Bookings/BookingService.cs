namespace Horafy.Domain.Entities.Bookings;

public sealed class BookingService
{
    private BookingService() { }

    public Guid    Id              { get; private set; } = Guid.NewGuid();
    public Guid    BookingId       { get; private set; }
    public Guid    ServiceId       { get; private set; }
    public string  ServiceName     { get; private set; } = default!;
    public int     DurationMinutes { get; private set; }
    public decimal Price           { get; private set; }

    /// <summary>
    /// Item de locação referenciado por esta linha, quando a reserva é do tipo
    /// <see cref="BookingKind.Rental"/>. Null em linhas de agendamento.
    /// </summary>
    public Guid?   RentableItemId  { get; private set; }

    /// <summary>Unidades reservadas (locação). Sempre 1 em linhas de agendamento.</summary>
    public int     Quantity        { get; private set; } = 1;

    internal static BookingService Create(
        Guid bookingId,
        Guid serviceId,
        string serviceName,
        int durationMinutes,
        decimal price,
        Guid? rentableItemId = null,
        int quantity = 1) =>
        new()
        {
            BookingId       = bookingId,
            ServiceId       = serviceId,
            ServiceName     = serviceName.Trim(),
            DurationMinutes = durationMinutes,
            Price           = price,
            RentableItemId  = rentableItemId,
            Quantity        = quantity < 1 ? 1 : quantity
        };
}
