using Horafy.Shared;

namespace Horafy.Application.Features.Bookings;

public static class BookingErrors
{
    public static readonly Error NotFound = new(
        "Booking.NotFound", "Agendamento não encontrado.", ErrorType.NotFound);

    public static readonly Error Conflict = new(
        "Booking.Conflict",
        "O recurso já possui um agendamento neste horário.",
        ErrorType.Conflict);

    public static readonly Error ResourceNotFound = new(
        "Booking.ResourceNotFound", "Recurso não encontrado.", ErrorType.NotFound);

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

    public static readonly Error SlotNotAvailable = new(
        "Booking.SlotNotAvailable",
        "O horário selecionado não está disponível.",
        ErrorType.Conflict);

    public static readonly Error CancellationWindowClosed = new(
        "Booking.CancellationWindowClosed",
        "O prazo mínimo para cancelamento já passou.",
        ErrorType.Validation);

    public static readonly Error CancellationNotAllowed = new(
        "Booking.CancellationNotAllowed",
        "O cancelamento pelo cliente não é permitido neste estabelecimento.",
        ErrorType.Validation);
}
