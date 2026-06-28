namespace Horafy.Application.Interfaces;

/// <summary>
/// Leitura agregada da carteira de clientes do tenant, derivada dos agendamentos.
/// Não há entidade Customer dedicada: o cliente é identificado pelo CustomerId
/// (User) e pelos dados desnormalizados nas reservas.
/// </summary>
public interface ICustomerListReader
{
    Task<IReadOnlyList<CustomerExportRecord>> GetCustomersAsync(CancellationToken ct = default);
}

public sealed record CustomerExportRecord(
    Guid            CustomerId,
    string          Name,
    string          Email,
    string?         Phone,
    int             BookingCount,
    DateTimeOffset? LastBookingAt,
    decimal         TotalSpent);
