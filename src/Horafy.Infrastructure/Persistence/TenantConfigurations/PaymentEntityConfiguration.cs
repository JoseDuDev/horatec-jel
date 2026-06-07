using Horafy.Domain.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class PaymentEntityConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PreferenceId).IsRequired().HasMaxLength(100);
        builder.Property(p => p.MpPaymentId).HasMaxLength(100);
        builder.HasIndex(p => p.MpPaymentId)
            .IsUnique()
            .HasFilter("mp_payment_id IS NOT NULL")
            .HasDatabaseName("uq_payments_mp_payment_id");
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32)
            .HasDefaultValue(PaymentStatus.Pending);
        builder.Property(p => p.Amount).HasColumnType("numeric(10,2)");
        builder.Property(p => p.DepositAmount).HasColumnType("numeric(10,2)");
        builder.Property(p => p.PaymentUrl).HasMaxLength(500);
        builder.HasIndex(p => p.BookingId).HasDatabaseName("ix_payments_booking_id");
        builder.Property(p => p.VoucherCode).HasMaxLength(50);
        builder.Property(p => p.VoucherDiscountAmount).HasColumnType("numeric(10,2)");
        builder.Property(p => p.WalletAmount).HasColumnType("numeric(10,2)");
    }
}
