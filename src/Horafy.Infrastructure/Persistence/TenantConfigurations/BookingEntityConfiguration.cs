using Horafy.Domain.Entities.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class BookingEntityConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.ResourceName).IsRequired().HasMaxLength(150).HasDefaultValue("");
        builder.Property(b => b.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(b => b.CustomerEmail).IsRequired().HasMaxLength(256);
        builder.Property(b => b.CustomerPhone).HasMaxLength(20);
        builder.Property(b => b.Notes).HasMaxLength(1000);
        builder.Property(b => b.CancellationReason).HasMaxLength(500);

        builder.Property(b => b.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(BookingKind.Appointment);

        builder.Property(b => b.RentalStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(b => b.SecurityDeposit)
            .HasColumnType("numeric(10,2)")
            .HasDefaultValue(0m);

        builder.Property(b => b.LateFee)
            .HasColumnType("numeric(10,2)")
            .HasDefaultValue(0m);

        builder.Property(b => b.DepositRefunded)
            .HasColumnType("numeric(10,2)")
            .HasDefaultValue(0m);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(b => b.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(BookingPaymentStatus.NotRequired);

        builder.Property(b => b.RecurrenceGroupId);
        builder.Property(b => b.ExpiresAt);

        builder.Property(b => b.Source).HasMaxLength(40);
        builder.Property(b => b.ExternalId).HasMaxLength(128);

        // Idempotência por integração: ExternalId único quando presente.
        builder.HasIndex(b => b.ExternalId)
            .IsUnique()
            .HasDatabaseName("ix_bookings_external_id")
            .HasFilter("external_id IS NOT NULL");

        builder.HasIndex(b => b.RecurrenceGroupId)
            .HasDatabaseName("ix_bookings_recurrence_group")
            .HasFilter("recurrence_group_id IS NOT NULL");

        builder.HasIndex(b => new { b.ResourceId, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_resource_scheduled");

        builder.HasIndex(b => new { b.CustomerId, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_customer_scheduled");

        builder.HasIndex(b => b.Status)
            .HasDatabaseName("ix_bookings_status");

        // Lembretes de agendamento e relatórios filtram por Status + intervalo de ScheduledAt.
        builder.HasIndex(b => new { b.Status, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_status_scheduled");

        // Lembretes de locação filtram por Kind + RentalStatus + intervalo de EndsAt.
        builder.HasIndex(b => new { b.Kind, b.RentalStatus, b.EndsAt })
            .HasDatabaseName("ix_bookings_kind_rentalstatus_ends");
    }
}
