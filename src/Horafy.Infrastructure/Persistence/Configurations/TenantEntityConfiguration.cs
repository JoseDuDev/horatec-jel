using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Tenants;
using Horafy.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API configuration para a entidade Tenant.
/// Tenant fica no schema "public" (compartilhado), pois é a tabela
/// usada para resolver qual schema/tenant ativar em cada requisição.
/// </summary>
public sealed class TenantEntityConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", "public");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("ix_tenants_slug");

        builder.Property(t => t.CustomDomain)
            .HasMaxLength(253);

        builder.HasIndex(t => t.CustomDomain)
            .IsUnique()
            .HasFilter("custom_domain IS NOT NULL")
            .HasDatabaseName("ix_tenants_custom_domain");

        builder.Property(t => t.Email).HasMaxLength(256);
        builder.Property(t => t.Phone).HasMaxLength(30);
        builder.Property(t => t.Address).HasMaxLength(500);
        builder.Property(t => t.City).HasMaxLength(100);
        builder.Property(t => t.State).HasMaxLength(50);
        builder.Property(t => t.ZipCode).HasMaxLength(10);
        builder.Property(t => t.TimeZoneId).HasMaxLength(100);
        builder.Property(t => t.Locale).HasMaxLength(10);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.Plan)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.Vertical)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Flags persistidas como int; default "ambos" (Appointments|Rentals = 3) preserva
        // tenants existentes ao aplicar a migração.
        builder.Property(t => t.Capabilities)
            .HasConversion<int>()
            .HasDefaultValue(TenantCapability.Appointments | TenantCapability.Rentals);

        // TenantTheme como owned entity (serializado em colunas da mesma tabela)
        builder.OwnsOne(t => t.Theme, themeBuilder =>
        {
            themeBuilder.Property(th => th.PrimaryColor).HasMaxLength(20);
            themeBuilder.Property(th => th.SecondaryColor).HasMaxLength(20);
            themeBuilder.Property(th => th.BackgroundColor).HasMaxLength(20);
            themeBuilder.Property(th => th.TextColor).HasMaxLength(20);
            themeBuilder.Property(th => th.FontFamily).HasMaxLength(100);
            themeBuilder.Property(th => th.LogoUrl).HasMaxLength(2000);
            themeBuilder.Property(th => th.FaviconUrl).HasMaxLength(2000);
            themeBuilder.Property(th => th.BannerUrl).HasMaxLength(2000);
            themeBuilder.Property(th => th.BannerText).HasMaxLength(500);
            themeBuilder.Property(th => th.InstagramUrl).HasMaxLength(500);
            themeBuilder.Property(th => th.WhatsAppNumber).HasMaxLength(30);
            themeBuilder.Property(th => th.FacebookUrl).HasMaxLength(500);
            themeBuilder.Property(th => th.SectionsOrder).HasMaxLength(200);
        });

        builder.OwnsOne(t => t.CancellationPolicy, policyBuilder =>
        {
            policyBuilder.Property(p => p.MinCancellationHours)
                .HasDefaultValue(0);
            policyBuilder.Property(p => p.CancellationFeePercent)
                .HasColumnType("numeric(5,2)")
                .HasDefaultValue(0m);
            policyBuilder.Property(p => p.AllowCustomerCancellation)
                .HasDefaultValue(true);
        });

        builder.OwnsOne(t => t.PaymentSettings, ps =>
        {
            ps.Property(p => p.RequiresPayment).HasDefaultValue(false);
            ps.Property(p => p.DepositMode)
              .HasConversion<string>().HasMaxLength(30)
              .HasDefaultValue(DepositMode.None);
            ps.Property(p => p.DepositValue)
              .HasColumnType("numeric(10,2)").HasDefaultValue(0m);
        });

        builder.OwnsOne(t => t.LoyaltySettings, ls =>
        {
            ls.Property(l => l.IsEnabled).HasDefaultValue(false);
            ls.Property(l => l.CreditRatePercent)
              .HasColumnType("numeric(5,2)").HasDefaultValue(0m);
            ls.Property(l => l.MinBookingAmount)
              .HasColumnType("numeric(10,2)").HasDefaultValue(0m);
        });

        // Lembretes automáticos. Defaults preservam o comportamento histórico (D-1 24h / H-2 2h).
        builder.OwnsOne(t => t.ReminderSettings, rs =>
        {
            rs.Property(r => r.Enabled).HasDefaultValue(true);
            rs.Property(r => r.FirstReminderHours).HasDefaultValue(24);
            rs.Property(r => r.SecondReminderHours).HasDefaultValue(2);
        });

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.IsDeleted).HasDefaultValue(false);
    }
}

/// <summary>
/// Configuração da tabela outbox_messages no schema public.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", "public");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Type).IsRequired().HasMaxLength(500);
        builder.Property(o => o.Content).IsRequired();
        builder.Property(o => o.Error).HasMaxLength(2000);

        builder.HasIndex(o => o.ProcessedAt)
            .HasFilter("processed_at IS NULL")
            .HasDatabaseName("ix_outbox_unprocessed");
    }
}
