using Horafy.Domain.Entities.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.Configurations;

internal sealed class IntegrationApiKeyEntityConfiguration
    : IEntityTypeConfiguration<IntegrationApiKey>
{
    public void Configure(EntityTypeBuilder<IntegrationApiKey> builder)
    {
        builder.ToTable("integration_api_keys", "public");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.TenantId).IsRequired();
        builder.HasIndex(k => k.TenantId)
            .HasDatabaseName("ix_integration_api_keys_tenant");

        builder.Property(k => k.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(k => k.KeyPrefix)
            .IsRequired()
            .HasMaxLength(64);
        builder.HasIndex(k => k.KeyPrefix)
            .IsUnique()
            .HasDatabaseName("ix_integration_api_keys_prefix");

        builder.Property(k => k.KeyHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(k => k.Scopes)
            .HasColumnType("text")
            .HasDefaultValue(string.Empty);

        builder.Property(k => k.IsActive);
        builder.Property(k => k.LastUsedAt);
        builder.Property(k => k.RevokedAt);

        builder.Property(k => k.CreatedAt);
        builder.Property(k => k.UpdatedAt);
        builder.Property(k => k.IsDeleted);
        builder.Property(k => k.DeletedAt);
    }
}
