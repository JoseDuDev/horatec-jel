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

        builder.Property(u => u.Phone)
            .HasMaxLength(20);

        // Não-único: o mesmo número pode existir em tenants diferentes (a
        // unicidade por tenant é garantida na aplicação). Suporta o lookup
        // do login por celular.
        builder.HasIndex(u => u.Phone)
            .HasDatabaseName("ix_users_phone");

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

        // Token de "esqueci minha senha": guardamos o hash SHA-256 (hex, 64 chars),
        // nunca o token puro — ele fica persistido em Postgres/backups.
        builder.Property(u => u.PasswordResetTokenHash)
            .HasMaxLength(64);

        builder.Property(u => u.PasswordResetExpiresAt);

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
