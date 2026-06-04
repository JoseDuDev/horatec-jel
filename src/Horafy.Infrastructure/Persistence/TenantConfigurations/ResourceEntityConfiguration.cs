using Horafy.Domain.Entities.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class ResourceEntityConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("resources");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(150);
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Email).HasMaxLength(256);
        builder.Property(r => r.Phone).HasMaxLength(20);
        builder.Property(r => r.Specialty).HasMaxLength(100);
        builder.Property(r => r.Bio).HasMaxLength(500);
        builder.Property(r => r.AvatarUrl).HasMaxLength(500);

        builder.HasIndex(r => r.Type).HasDatabaseName("ix_resources_type");
        builder.HasIndex(r => r.IsActive).HasDatabaseName("ix_resources_is_active");
    }
}
