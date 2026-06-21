namespace Horafy.Domain.Entities.Bookings;

/// <summary>
/// Distingue o modelo de reserva: agendamento por horário (comportamento atual)
/// versus locação por período de 1 ou mais dias.
/// </summary>
public enum BookingKind
{
    /// <summary>Agendamento por horário, intra-dia (corte, consulta, etc.).</summary>
    Appointment = 0,

    /// <summary>Locação de item por período (1+ dias).</summary>
    Rental = 1,
}
