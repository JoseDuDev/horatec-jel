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
