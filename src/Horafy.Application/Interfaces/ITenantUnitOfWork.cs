using System.Data;

namespace Horafy.Application.Interfaces;

public interface ITenantUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<ITransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa <paramref name="operation"/> dentro de uma transação, de forma compatível
    /// com a estratégia de retry do Npgsql (envolve em CreateExecutionStrategy). A operação
    /// pode ser re-executada em caso de falha de serialização — deve ser idempotente.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}
