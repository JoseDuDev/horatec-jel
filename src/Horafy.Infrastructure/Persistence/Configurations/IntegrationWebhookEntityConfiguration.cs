using Horafy.Domain.Entities.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.Configurations;

internal sealed class IntegrationWebhookEntityConfiguration
    : IEntityTypeConfiguration<IntegrationWebhook>
{
    public void Configure(EntityTypeBuilder<IntegrationWebhook> builder)
    {
        builder.ToTable("integration_webhooks", "public");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.TenantId).IsRequired();
        builder.HasIndex(w => w.TenantId)
            .IsUnique()
            .HasDatabaseName("ix_integration_webhooks_tenant");

        builder.Property(w => w.Url)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.Secret)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(w => w.IsActive);

        builder.Property(w => w.CreatedAt);
        builder.Property(w => w.UpdatedAt);
        builder.Property(w => w.IsDeleted);
        builder.Property(w => w.DeletedAt);
    }
}
