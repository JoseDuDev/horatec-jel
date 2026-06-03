using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Horafy.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor do EF Core que preenche automaticamente os campos de auditoria
/// (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) em todas as entidades que
/// herdam de BaseEntity, sem necessidade de código explícito nos handlers.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentTenantService tenantService)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null) return;

        var now = DateTimeOffset.UtcNow;
        var currentUser = tenantService.TenantId?.ToString() ?? "system";

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = currentUser;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUser;
                    // Impede que campos de criação sejam sobrescritos por acidente
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    break;
            }
        }
    }
}
