namespace Horafy.Domain.Entities.Bookings;

/// <summary>
/// Estágio do ciclo de vida de uma locação (Kind = Rental). Independente de
/// <see cref="BookingStatus"/> (Pending/Confirmed via pagamento): controla
/// retirada e devolução do item físico.
/// </summary>
public enum RentalLifecycle
{
    /// <summary>Reservada, item ainda não retirado.</summary>
    Reserved = 0,

    /// <summary>Item retirado pelo cliente (em posse).</summary>
    PickedUp = 1,

    /// <summary>Item devolvido — libera o estoque.</summary>
    Returned = 2,
}
