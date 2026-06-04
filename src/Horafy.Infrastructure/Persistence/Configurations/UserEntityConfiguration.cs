using Horafy.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.Configurations;

internal sealed class UserEntityConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "public");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email");

        builder.Property(u => u.Name)
            .HasMaxLength(150);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(u => u.GoogleId)
            .HasMaxLength(128);

        builder.HasIndex(u => u.GoogleId)
            .IsUnique()
            .HasFilter("google_id IS NOT NULL")
            .HasDatabaseName("ix_users_google_id");

        builder.Property(u => u.AppleId)
            .HasMaxLength(128);

        builder.HasIndex(u => u.AppleId)
            .IsUnique()
            .HasFilter("apple_id IS NOT NULL")
            .HasDatabaseName("ix_users_apple_id");

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(256);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(u => new { u.TenantId, u.Role })
            .HasDatabaseName("ix_users_tenant_role");

        // Mapeamento do campo privado _permissionsRaw
        builder.Property<string>("_permissionsRaw")
            .HasField("_permissionsRaw")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("permissions")
            .HasColumnType("text")
            .HasDefaultValue(string.Empty);

        builder.Property(u => u.IsEmailVerified);
        builder.Property(u => u.LastLoginAt);
        builder.Property(u => u.CreatedAt);
        builder.Property(u => u.UpdatedAt);
        builder.Property(u => u.IsDeleted);
        builder.Property(u => u.DeletedAt);
    }
}
