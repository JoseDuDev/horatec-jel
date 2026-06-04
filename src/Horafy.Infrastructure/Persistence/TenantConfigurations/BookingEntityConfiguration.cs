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

        builder.Property(b => b.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(b => b.CustomerEmail).IsRequired().HasMaxLength(256);
        builder.Property(b => b.Notes).HasMaxLength(1000);
        builder.Property(b => b.CancellationReason).HasMaxLength(500);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(b => new { b.ResourceId, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_resource_scheduled");

        builder.HasIndex(b => new { b.CustomerId, b.ScheduledAt })
            .HasDatabaseName("ix_bookings_customer_scheduled");

        builder.HasIndex(b => b.Status)
            .HasDatabaseName("ix_bookings_status");
    }
}
