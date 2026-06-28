using Horafy.Shared;

namespace Horafy.Application.Features.Availability;

public static class AvailabilityErrors
{
    public static readonly Error ResourceNotFound = new(
        "Availability.ResourceNotFound", "Recurso não encontrado.", ErrorType.NotFound);

    public static readonly Error ServiceNotFound = new(
        "Availability.ServiceNotFound", "Serviço não encontrado.", ErrorType.NotFound);

    public static readonly Error ServiceAlreadyLinked = new(
        "Availability.ServiceAlreadyLinked",
        "O serviço já está vinculado a este recurso.",
        ErrorType.Conflict);

    public static readonly Error ServiceNotLinked = new(
        "Availability.ServiceNotLinked",
        "O serviço não está vinculado a este recurso.",
        ErrorType.NotFound);

    public static readonly Error ExceptionNotFound = new(
        "Availability.ExceptionNotFound", "Exceção não encontrada.", ErrorType.NotFound);

    public static readonly Error HolidayNotFound = new(
        "Availability.HolidayNotFound", "Feriado não encontrado.", ErrorType.NotFound);
}
