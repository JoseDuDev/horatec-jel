using Horafy.Domain.Entities.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class HolidayEntityConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("holidays");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Name).HasMaxLength(200).IsRequired();
        builder.Property(h => h.Reason).HasMaxLength(500);
        builder.HasIndex(h => h.Date).HasDatabaseName("ix_holidays_date");
    }
}
