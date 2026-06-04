using Horafy.Shared;

namespace Horafy.Application.Features.Resources;

public static class ResourceErrors
{
    public static readonly Error NotFound = new(
        "Resource.NotFound", "Recurso não encontrado.", ErrorType.NotFound);

    public static readonly Error NameAlreadyExists = new(
        "Resource.NameAlreadyExists", "Já existe um recurso com este nome.", ErrorType.Conflict);
}
