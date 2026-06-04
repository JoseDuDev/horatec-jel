using Horafy.Shared;

namespace Horafy.Application.Features.Waitlist;

public static class WaitlistErrors
{
    public static readonly Error NotFound = new(
        "Waitlist.NotFound", "Entrada na fila não encontrada.", ErrorType.NotFound);

    public static readonly Error AlreadyInQueue = new(
        "Waitlist.AlreadyInQueue",
        "Você já está na fila para este serviço nesta data.",
        ErrorType.Conflict);

    public static readonly Error ServiceNotFound = new(
        "Waitlist.ServiceNotFound", "Serviço não encontrado.", ErrorType.NotFound);

    public static readonly Error ResourceNotFound = new(
        "Waitlist.ResourceNotFound", "Recurso não encontrado.", ErrorType.NotFound);
}
