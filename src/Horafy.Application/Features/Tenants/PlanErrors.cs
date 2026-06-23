using Horafy.Shared;

namespace Horafy.Application.Features.Tenants;

/// <summary>
/// Erros de capacidade (módulo não contratado) e de limite de plano (quota atingida).
/// </summary>
public static class PlanErrors
{
    public static readonly Error AppointmentsNotEnabled = new(
        "Plan.AppointmentsNotEnabled",
        "Este estabelecimento não tem o módulo de agendamento habilitado no plano.",
        ErrorType.Conflict);

    public static readonly Error RentalsNotEnabled = new(
        "Plan.RentalsNotEnabled",
        "Este estabelecimento não tem o módulo de locação habilitado no plano.",
        ErrorType.Conflict);

    public static Error ServiceLimitReached(int max) => new(
        "Plan.ServiceLimitReached",
        $"Limite de serviços do plano atingido ({max}). Faça upgrade para cadastrar mais.",
        ErrorType.Conflict);

    public static Error ResourceLimitReached(int max) => new(
        "Plan.ResourceLimitReached",
        $"Limite de recursos do plano atingido ({max}). Faça upgrade para cadastrar mais.",
        ErrorType.Conflict);

    public static Error RentableItemLimitReached(int max) => new(
        "Plan.RentableItemLimitReached",
        $"Limite de itens de locação do plano atingido ({max}). Faça upgrade para cadastrar mais.",
        ErrorType.Conflict);
}
