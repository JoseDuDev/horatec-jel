using Horafy.Domain.Entities.Professionals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class ProfessionalEntityConfiguration : IEntityTypeConfiguration<Professional>
{
    public void Configure(EntityTypeBuilder<Professional> builder)
    {
        builder.ToTable("professionals");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Email).HasMaxLength(256);
        builder.Property(p => p.Phone).HasMaxLength(20);
        builder.Property(p => p.Specialty).HasMaxLength(100);
        builder.Property(p => p.Bio).HasMaxLength(500);
        builder.Property(p => p.AvatarUrl).HasMaxLength(500);

        builder.HasIndex(p => p.IsActive).HasDatabaseName("ix_professionals_is_active");
    }
}
