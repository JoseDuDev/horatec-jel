using System.Linq.Expressions;
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories.Base;

/// <summary>
/// Implementação genérica do repositório usando EF Core.
/// Aceita qualquer DbContext, permitindo uso com HorafyDbContext (schema public)
/// e TenantDbContext (schema tenant_{slug}).
/// </summary>
public abstract class BaseRepository<T, TContext>(TContext context) : IRepository<T>
    where T : BaseEntity
    where TContext : DbContext
{
    protected readonly TContext  Context = context;
    protected readonly DbSet<T> DbSet   = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().ToListAsync(cancellationToken);

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(predicate, cancellationToken);

    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        predicate is null
            ? await DbSet.CountAsync(cancellationToken)
            : await DbSet.CountAsync(predicate, cancellationToken);

    public void Add(T entity)    => DbSet.Add(entity);

    /// <summary>
    /// Só anexa entidade DESTACADA (as buscas acima são AsNoTracking). Se o contexto
    /// já rastreia a entidade, o change tracker detecta sozinho o que mudou — e o
    /// DbSet.Update atrapalharia: marca como Modified todo filho novo do grafo que já
    /// tem Id (os Ids nascem no construtor), e o SaveChanges emite UPDATE de linha
    /// que não existe (0 linhas, DbUpdateConcurrencyException). Era o que quebrava
    /// todo movimento de carteira (ver WalletPersistenceTests).
    /// </summary>
    public void Update(T entity)
    {
        if (Context.Entry(entity).State == EntityState.Detached)
            DbSet.Update(entity);
    }

    public void Remove(T entity) => DbSet.Remove(entity);
}
