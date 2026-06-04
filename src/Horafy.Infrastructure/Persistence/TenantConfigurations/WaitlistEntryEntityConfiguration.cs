using Horafy.Domain.Entities.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class WaitlistEntryEntityConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("waitlist_entries");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(w => w.CustomerEmail).IsRequired().HasMaxLength(256);

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(w => new { w.ServiceId, w.ResourceId, w.PreferredDate })
            .HasDatabaseName("ix_waitlist_service_resource_date");

        builder.HasIndex(w => w.CustomerId)
            .HasDatabaseName("ix_waitlist_customer");
    }
}
