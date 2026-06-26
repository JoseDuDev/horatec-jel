namespace Horafy.Domain.Entities.Bookings;

/// <summary>
/// Origens conhecidas de um agendamento criado por integração.
/// Fluxos nativos (portal/admin) deixam <see cref="Booking.Source"/> nulo.
/// </summary>
public static class BookingSource
{
    public const string Atendefy = "atendefy";
}
