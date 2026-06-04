using Horafy.Domain.Entities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class ServiceEntityConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        // Sem HasSchema — a tabela será resolvida pelo search_path do tenant
        builder.ToTable("services");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Description).HasMaxLength(1000);
        builder.Property(s => s.Price).HasColumnType("numeric(10,2)");
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.Property(s => s.IsActive);

        builder.HasIndex(s => s.Name).HasDatabaseName("ix_services_name");
        builder.HasIndex(s => s.IsActive).HasDatabaseName("ix_services_is_active");
    }
}
