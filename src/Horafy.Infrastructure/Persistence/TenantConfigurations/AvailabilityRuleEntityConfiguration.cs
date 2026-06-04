using Horafy.Domain.Entities.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class AvailabilityRuleEntityConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("availability_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DayOfWeek).HasConversion<int>();
        builder.HasIndex(r => new { r.ResourceId, r.DayOfWeek })
            .IsUnique()
            .HasDatabaseName("ix_availability_rules_resource_day");
    }
}
