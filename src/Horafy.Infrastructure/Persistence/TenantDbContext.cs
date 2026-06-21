using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Entities.Notifications;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Reviews;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Vouchers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;
using WalletTransaction = Horafy.Domain.Entities.Wallet.WalletTransaction;

namespace Horafy.Infrastructure.Persistence;

public sealed class TenantDbContext : DbContext
{
    private readonly IPublisher? _publisher;

    public DbSet<Service>               Services               => Set<Service>();
    public DbSet<Resource>              Resources              => Set<Resource>();
    public DbSet<ResourceService>       ResourceServices       => Set<ResourceService>();
    public DbSet<Booking>               Bookings               => Set<Booking>();
    public DbSet<BusinessHours>         BusinessHours          => Set<BusinessHours>();
    public DbSet<AvailabilityRule>      AvailabilityRules      => Set<AvailabilityRule>();
    public DbSet<AvailabilityException> AvailabilityExceptions => Set<AvailabilityException>();
    public DbSet<WaitlistEntry>         WaitlistEntries        => Set<WaitlistEntry>();
    public DbSet<BookingService>        BookingServices        => Set<BookingService>();
    public DbSet<Payment>               Payments               => Set<Payment>();
    public DbSet<NotificationTemplate>  NotificationTemplates  => Set<NotificationTemplate>();
    public DbSet<Review>                Reviews                => Set<Review>();
    public DbSet<FavoriteService>       FavoriteServices       => Set<FavoriteService>();
    public DbSet<WalletEntity>          Wallets                => Set<WalletEntity>();
    public DbSet<WalletTransaction>     WalletTransactions     => Set<WalletTransaction>();
    public DbSet<Voucher>               Vouchers               => Set<Voucher>();
    public DbSet<RentableItem>          RentableItems          => Set<RentableItem>();

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

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TenantDbContext).Assembly,
            t => t.Namespace?.Contains("TenantConfigurations") is true);

        modelBuilder.Entity<Booking>()
            .HasMany(b => b.Services)
            .WithOne()
            .HasForeignKey(bs => bs.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Booking>()
            .Navigation(b => b.Services)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

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

        // Limpa os eventos ANTES de publicar: um handler pode chamar SaveChangesAsync
        // de novo (re-entrante) e re-coletaria os mesmos eventos ainda não limpos,
        // causando dispatch duplicado e conflito de tracking (duas instâncias da mesma entidade).
        ChangeTracker
            .Entries<BaseEntity>()
            .ToList()
            .ForEach(e => e.Entity.ClearDomainEvents());

        if (_publisher is not null)
            foreach (var ev in domainEvents)
                await _publisher.Publish(ev, cancellationToken);

        return result;
    }
}
