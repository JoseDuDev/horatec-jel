using Horafy.Domain.Entities.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class AvailabilityExceptionEntityConfiguration
    : IEntityTypeConfiguration<AvailabilityException>
{
    public void Configure(EntityTypeBuilder<AvailabilityException> builder)
    {
        builder.ToTable("availability_exceptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.HasIndex(e => new { e.ResourceId, e.Date })
            .IsUnique()
            .HasDatabaseName("ix_availability_exceptions_resource_date");
    }
}
