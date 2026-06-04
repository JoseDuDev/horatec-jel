using Horafy.Shared;

namespace Horafy.Application.Features.Tenants;

public static class TenantErrors
{
    public static readonly Error SlugAlreadyTaken = new(
        "Tenant.SlugAlreadyTaken",
        "Este slug já está em uso. Escolha outro identificador.",
        ErrorType.Conflict);

    public static readonly Error NotFound = new(
        "Tenant.NotFound",
        "Estabelecimento não encontrado.",
        ErrorType.NotFound);

    public static readonly Error OwnerEmailAlreadyRegistered = new(
        "Tenant.OwnerEmailAlreadyRegistered",
        "Já existe uma conta com este e-mail.",
        ErrorType.Conflict);

    public static readonly Error DomainAlreadyTaken = new(
        "Tenant.DomainAlreadyTaken",
        "Este domínio já está vinculado a outro estabelecimento.",
        ErrorType.Conflict);

    public static readonly Error Suspended = new(
        "Tenant.Suspended",
        "Esta conta está suspensa.",
        ErrorType.Unauthorized);
}
