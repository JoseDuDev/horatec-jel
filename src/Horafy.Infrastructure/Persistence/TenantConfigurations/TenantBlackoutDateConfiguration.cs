using Horafy.Domain.Entities.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class TenantBlackoutDateConfiguration : IEntityTypeConfiguration<TenantBlackoutDate>
{
    public void Configure(EntityTypeBuilder<TenantBlackoutDate> builder)
    {
        builder.ToTable("tenant_blackout_dates");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Reason).HasMaxLength(500);
        builder.HasIndex(b => b.Date)
            .IsUnique()
            .HasFilter("is_deleted = FALSE")
            .HasDatabaseName("uq_tenant_blackout_dates_date");
    }
}
