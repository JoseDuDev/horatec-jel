namespace Horafy.Domain.Entities.Bookings;

public sealed class BookingService
{
    private BookingService() { }

    public Guid   Id              { get; private set; } = Guid.NewGuid();
    public Guid   BookingId       { get; private set; }
    public Guid   ServiceId       { get; private set; }
    public string ServiceName     { get; private set; } = default!;
    public int    DurationMinutes { get; private set; }

    internal static BookingService Create(
        Guid bookingId, Guid serviceId, string serviceName, int durationMinutes) =>
        new()
        {
            BookingId       = bookingId,
            ServiceId       = serviceId,
            ServiceName     = serviceName.Trim(),
            DurationMinutes = durationMinutes
        };
}
