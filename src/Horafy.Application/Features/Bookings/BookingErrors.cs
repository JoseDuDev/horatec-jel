using Horafy.Shared;

namespace Horafy.Application.Features.Bookings;

public static class BookingErrors
{
    public static readonly Error NotFound = new(
        "Booking.NotFound", "Agendamento não encontrado.", ErrorType.NotFound);

    public static readonly Error Conflict = new(
        "Booking.Conflict",
        "O profissional já possui um agendamento neste horário.",
        ErrorType.Conflict);

    public static readonly Error ProfessionalNotFound = new(
        "Booking.ProfessionalNotFound", "Profissional não encontrado.", ErrorType.NotFound);

    public static readonly Error ServiceNotFound = new(
        "Booking.ServiceNotFound", "Serviço não encontrado.", ErrorType.NotFound);

    public static readonly Error NotOwner = new(
        "Booking.NotOwner",
        "Você não tem permissão para alterar este agendamento.",
        ErrorType.Unauthorized);

    public static readonly Error PastDate = new(
        "Booking.PastDate",
        "Não é possível agendar para uma data no passado.",
        ErrorType.Validation);
}
