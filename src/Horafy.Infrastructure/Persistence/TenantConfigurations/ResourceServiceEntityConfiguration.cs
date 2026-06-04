using Horafy.Domain.Entities.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class ResourceServiceEntityConfiguration : IEntityTypeConfiguration<ResourceService>
{
    public void Configure(EntityTypeBuilder<ResourceService> builder)
    {
        builder.ToTable("resource_services");
        builder.HasKey(rs => rs.Id);
        builder.HasIndex(rs => new { rs.ResourceId, rs.ServiceId })
            .IsUnique()
            .HasDatabaseName("ix_resource_services_resource_service");
    }
}
