using Horafy.Domain.Entities.Rentals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class RentableItemEntityConfiguration : IEntityTypeConfiguration<RentableItem>
{
    public void Configure(EntityTypeBuilder<RentableItem> builder)
    {
        // Sem HasSchema — a tabela será resolvida pelo search_path do tenant
        builder.ToTable("rentable_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Description).HasMaxLength(1000);
        builder.Property(i => i.Category).HasMaxLength(100);
        builder.Property(i => i.Quantity);
        builder.Property(i => i.DailyRate).HasColumnType("numeric(10,2)");
        builder.Property(i => i.SecurityDeposit).HasColumnType("numeric(10,2)");
        builder.Property(i => i.BufferDays);
        builder.Property(i => i.ImageUrl).HasMaxLength(2000);
        builder.Property(i => i.IsActive);

        // A galeria é parte do agregado: só o RentableItem adiciona e remove foto,
        // e o EF escreve direto no campo (a propriedade Images é só leitura).
        builder.HasMany(i => i.Images)
            .WithOne()
            .HasForeignKey(image => image.RentableItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Images)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(i => i.Name).HasDatabaseName("ix_rentable_items_name");
        builder.HasIndex(i => i.IsActive).HasDatabaseName("ix_rentable_items_is_active");
    }
}
