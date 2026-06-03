using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Entities.Tenants;
using Horafy.Infrastructure.Persistence.Interceptors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Persistence;

/// <summary>
/// DbContext principal do Horafy.
///
/// Estratégia de multi-tenancy:
///   - Schema "public": tabelas globais (tenants, planos, configurações da plataforma)
///   - Schema "tenant_{slug}": tabelas isoladas por tenant (agendamentos, serviços, etc.)
///
/// O search_path do PostgreSQL é configurado via ICurrentTenantService
/// antes de cada operação, garantindo que o EF Core resolva as tabelas
/// no schema correto sem nenhuma modificação nos modelos.
/// </summary>
public sealed class HorafyDbContext : DbContext
{
    private readonly ICurrentTenantService? _tenantService;
    private readonly IPublisher? _publisher;

    // Tabelas globais (schema public)
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public HorafyDbContext(
        DbContextOptions<HorafyDbContext> options,
        ICurrentTenantService? tenantService = null,
        IPublisher? publisher = null)
        : base(options)
    {
        _tenantService = tenantService;
        _publisher = publisher;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas as configurações do assembly automaticamente
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HorafyDbContext).Assembly);

        // Global Query Filters: soft-delete — nenhuma entidade deletada retorna em queries
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isDeletedProperty = entityType.FindProperty("IsDeleted");
            if (isDeletedProperty is not null && isDeletedProperty.ClrType == typeof(bool))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, "IsDeleted");
                var condition = System.Linq.Expressions.Expression.Not(property);
                var lambda = System.Linq.Expressions.Expression.Lambda(condition, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Coleta domain events antes de salvar
        var domainEvents = ChangeTracker
            .Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Publica domain events via MediatR após commit
        if (_publisher is not null)
        {
            foreach (var domainEvent in domainEvents)
            {
                await _publisher.Publish(domainEvent, cancellationToken);
            }
        }

        // Limpa os eventos das entidades
        ChangeTracker
            .Entries<BaseEntity>()
            .ToList()
            .ForEach(e => e.Entity.ClearDomainEvents());

        return result;
    }
}
