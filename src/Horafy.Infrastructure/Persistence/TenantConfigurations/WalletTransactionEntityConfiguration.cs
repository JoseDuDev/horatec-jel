using Horafy.Domain.Entities.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class WalletTransactionEntityConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Amount).HasColumnType("numeric(12,2)");
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(255);
        builder.HasIndex(t => t.WalletId).HasDatabaseName("ix_wallet_transactions_wallet_id");
        builder.HasIndex(t => t.BookingId)
            .HasFilter("booking_id IS NOT NULL")
            .HasDatabaseName("ix_wallet_transactions_booking_id");

        builder.HasOne<Wallet>()
            .WithMany(w => w.Transactions)
            .HasForeignKey(t => t.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
