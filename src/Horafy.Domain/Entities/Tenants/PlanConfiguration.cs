using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Tenants;

/// <summary>
/// Limites de um plano, persistidos e editáveis pela plataforma. Quando existe uma linha
/// para o plano, ela sobrepõe os defaults em <see cref="PlanLimits.For"/>. Reside no schema
/// public (planos são globais). -1 = ilimitado.
/// </summary>
public sealed class PlanConfiguration : BaseEntity
{
    private PlanConfiguration() { } // EF Core

    public TenantPlan Plan             { get; private set; }
    public int        MaxServices      { get; private set; }
    public int        MaxResources     { get; private set; }
    public int        MaxRentableItems { get; private set; }

    public static PlanConfiguration Create(
        TenantPlan plan, int maxServices, int maxResources, int maxRentableItems) => new()
    {
        Plan             = plan,
        MaxServices      = maxServices,
        MaxResources     = maxResources,
        MaxRentableItems = maxRentableItems,
    };

    public void Update(int maxServices, int maxResources, int maxRentableItems)
    {
        MaxServices      = maxServices;
        MaxResources     = maxResources;
        MaxRentableItems = maxRentableItems;
        UpdatedAt        = DateTimeOffset.UtcNow;
    }

    public PlanLimits ToLimits() => new(MaxServices, MaxResources, MaxRentableItems);
}
