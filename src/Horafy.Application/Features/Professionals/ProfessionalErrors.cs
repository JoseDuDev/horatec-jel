using Horafy.Shared;

namespace Horafy.Application.Features.Professionals;

public static class ProfessionalErrors
{
    public static readonly Error NotFound = new(
        "Professional.NotFound", "Profissional não encontrado.", ErrorType.NotFound);
}
