using Horafy.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.Configurations;

internal sealed class NotificationLogEntityConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("notification_logs", "public");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.TenantSlug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.EventType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.Channel)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(n => n.Recipient)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(n => n.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasIndex(n => new { n.TenantSlug, n.SentAt })
            .HasDatabaseName("ix_notification_logs_tenant_sent");
    }
}
