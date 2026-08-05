using Horafy.Domain.Entities.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.Configurations;

internal sealed class PlatformLeadEntityConfiguration : IEntityTypeConfiguration<PlatformLead>
{
    public void Configure(EntityTypeBuilder<PlatformLead> builder)
    {
        builder.ToTable("platform_leads", "public");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.Phone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(l => l.Email)
            .HasMaxLength(256);

        builder.Property(l => l.BusinessType)
            .HasMaxLength(100);

        builder.Property(l => l.Message)
            .HasMaxLength(1000);

        builder.Property(l => l.Brand)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(l => new { l.Phone, l.CreatedAt })
            .HasDatabaseName("ix_platform_leads_phone_created");
    }
}
