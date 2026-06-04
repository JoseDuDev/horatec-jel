namespace Horafy.Application.Interfaces;

/// <summary>
/// Unit of Work para operações no schema do tenant atual.
/// Distinto de IUnitOfWork (que salva o schema public).
/// </summary>
public interface ITenantUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
