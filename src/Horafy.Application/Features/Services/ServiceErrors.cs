using Horafy.Shared;

namespace Horafy.Application.Features.Services;

public static class ServiceErrors
{
    public static readonly Error NotFound = new(
        "Service.NotFound", "Serviço não encontrado.", ErrorType.NotFound);

    public static readonly Error NameAlreadyExists = new(
        "Service.NameAlreadyExists", "Já existe um serviço com este nome.", ErrorType.Conflict);
}
