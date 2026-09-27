using Horafy.Domain.Entities.Rentals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class RentableItemImageEntityConfiguration : IEntityTypeConfiguration<RentableItemImage>
{
    public void Configure(EntityTypeBuilder<RentableItemImage> builder)
    {
        // Sem HasSchema — a tabela será resolvida pelo search_path do tenant
        builder.ToTable("rentable_item_images");

        builder.HasKey(i => i.Id);

        // O Id nasce no construtor (BaseEntity). Sem isto o EF supõe chave gerada
        // pelo banco, toma a foto nova adicionada a um item JÁ salvo por existente
        // e emite UPDATE em vez de INSERT — 0 linhas, DbUpdateConcurrencyException,
        // 500 no upload com o arquivo já no disco (produção, 26/09/2026). Mesmo
        // motivo do BookingService. Ver RentableItemImagePersistenceTests.
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.RentableItemId).IsRequired();
        builder.Property(i => i.Url).IsRequired().HasMaxLength(2000);
        builder.Property(i => i.SortOrder);

        builder.HasIndex(i => new { i.RentableItemId, i.SortOrder })
            .HasDatabaseName("ix_rentable_item_images_item_sort");
    }
}
