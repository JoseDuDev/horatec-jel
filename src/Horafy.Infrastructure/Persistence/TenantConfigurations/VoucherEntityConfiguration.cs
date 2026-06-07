using Horafy.Domain.Entities.Vouchers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class VoucherEntityConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("vouchers");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(v => v.Code)
            .IsUnique()
            .HasDatabaseName("uq_vouchers_code");
        builder.Property(v => v.DiscountType).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.DiscountValue).HasColumnType("numeric(10,2)");
        builder.Property(v => v.Description).HasMaxLength(255);
        builder.Property(v => v.IsActive).HasDefaultValue(true);
    }
}
