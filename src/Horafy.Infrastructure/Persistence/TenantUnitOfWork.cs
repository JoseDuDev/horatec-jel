using System.Data;
using Horafy.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Persistence;

internal sealed class TenantUnitOfWork(TenantDbContext context) : ITenantUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<ITransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var tx = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        return new EfTransaction(tx);
    }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        // A estratégia de retry do Npgsql não permite transação iniciada manualmente —
        // toda a operação (incluindo o commit) precisa rodar como uma unidade retentável.
        var strategy = context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
            var result = await operation(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return result;
        });
    }
}

file sealed class EfTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx)
    : ITransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        tx.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        tx.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => tx.DisposeAsync();
}
