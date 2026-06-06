using Horafy.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class NotificationTemplateEntityConfiguration
    : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.EventType).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.SubjectTemplate).HasMaxLength(300);
        builder.Property(t => t.BodyTemplate).IsRequired();
    }
}
