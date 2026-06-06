using Horafy.Domain.Entities.Favorites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class FavoriteServiceEntityConfiguration
    : IEntityTypeConfiguration<FavoriteService>
{
    public void Configure(EntityTypeBuilder<FavoriteService> builder)
    {
        builder.ToTable("favorite_services");
        builder.HasKey(f => f.Id);
        builder.HasIndex(f => new { f.CustomerId, f.ServiceId })
            .IsUnique()
            .HasFilter("is_deleted = FALSE")
            .HasDatabaseName("uq_favorite_services_customer_service");
        builder.HasIndex(f => f.CustomerId)
            .HasFilter("is_deleted = FALSE")
            .HasDatabaseName("ix_favorite_services_customer");
    }
}
