using Horafy.Domain.Entities.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class BusinessHoursEntityConfiguration : IEntityTypeConfiguration<BusinessHours>
{
    public void Configure(EntityTypeBuilder<BusinessHours> builder)
    {
        builder.ToTable("business_hours");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.DayOfWeek).HasConversion<int>();
        builder.HasIndex(b => b.DayOfWeek)
            .IsUnique()
            .HasDatabaseName("ix_business_hours_day");
    }
}
