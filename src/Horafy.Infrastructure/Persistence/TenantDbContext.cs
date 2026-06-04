using Horafy.Domain.Entities.Base;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Professionals;
using Horafy.Domain.Entities.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Persistence;

/// <summary>
/// DbContext para as tabelas do schema tenant_{slug}.
/// O search_path é configurado via connection string no momento da injeção,
/// garantindo que todas as queries operem no schema correto do tenant.
/// </summary>
public sealed class TenantDbContext : DbContext
{
    private readonly IPublisher? _publisher;

    public DbSet<Service>      Services      => Set<Service>();
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<Booking>      Bookings      => Set<Booking>();

    public TenantDbContext(
        DbContextOptions<TenantDbContext> options,
        IPublisher? publisher = null)
        : base(options)
    {
        _publisher = publisher;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica as configs de tenant (Service, Professional, Booking)
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TenantDbContext).Assembly,
            t => t.Namespace?.Contains("TenantConfigurations") is true);

        // Global Query Filter: soft-delete
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var prop = entityType.FindProperty("IsDeleted");
            if (prop is not null && prop.ClrType == typeof(bool))
            {
                var p  = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var pr = System.Linq.Expressions.Expression.Property(p, "IsDeleted");
                var c  = System.Linq.Expressions.Expression.Not(pr);
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(System.Linq.Expressions.Expression.Lambda(c, p));
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker
            .Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_publisher is not null)
            foreach (var ev in domainEvents)
                await _publisher.Publish(ev, cancellationToken);

        ChangeTracker
            .Entries<BaseEntity>()
            .ToList()
            .ForEach(e => e.Entity.ClearDomainEvents());

        return result;
    }
}
