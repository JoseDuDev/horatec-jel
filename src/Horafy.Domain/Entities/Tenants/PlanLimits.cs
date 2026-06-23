namespace Horafy.Domain.Entities.Tenants;

/// <summary>
/// Limites de cadastro por plano (pacote vendável). <see cref="Unlimited"/> (-1) = sem limite.
/// Os números ficam em código no MVP; podem virar configuráveis por tabela depois (Fase 4).
/// </summary>
public sealed record PlanLimits(int MaxServices, int MaxResources, int MaxRentableItems)
{
    public const int Unlimited = -1;

    public static PlanLimits For(TenantPlan plan) => plan switch
    {
        TenantPlan.Free         => new PlanLimits(MaxServices: 5,  MaxResources: 2, MaxRentableItems: 5),
        TenantPlan.Starter      => new PlanLimits(MaxServices: 10, MaxResources: 2, MaxRentableItems: 10),
        TenantPlan.Professional => new PlanLimits(MaxServices: 20, MaxResources: 3, MaxRentableItems: 20),
        TenantPlan.Enterprise   => new PlanLimits(Unlimited, Unlimited, Unlimited),
        _                       => new PlanLimits(5, 2, 5),
    };

    public bool ServicesReached(int current)      => MaxServices      >= 0 && current >= MaxServices;
    public bool ResourcesReached(int current)     => MaxResources     >= 0 && current >= MaxResources;
    public bool RentableItemsReached(int current) => MaxRentableItems >= 0 && current >= MaxRentableItems;
}
