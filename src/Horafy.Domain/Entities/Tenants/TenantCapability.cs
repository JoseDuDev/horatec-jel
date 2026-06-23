namespace Horafy.Domain.Entities.Tenants;

/// <summary>
/// Módulos (capacidades) que um tenant pode operar. Combinável: um estabelecimento
/// pode ter agendamento, locação, ou ambos. Definido pela plataforma ao contratar o pacote.
/// </summary>
[Flags]
public enum TenantCapability
{
    None         = 0,
    Appointments = 1,
    Rentals      = 2,
}
