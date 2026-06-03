using System.Linq.Expressions;
using Horafy.Domain.Entities.Base;
using Horafy.Shared;

namespace Horafy.Domain.Interfaces.Repositories;

/// <summary>
/// Interface genérica de repositório. Aplica o princípio ISP —
/// repositórios específicos só expõem o que precisam.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    void Add(T entity);
    void Update(T entity);
    void Remove(T entity);
}
