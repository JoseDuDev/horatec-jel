using Horafy.Domain.Entities.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class ReviewEntityConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Stars).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(1000);
        builder.Property(r => r.OwnerReply).HasMaxLength(1000);
        builder.HasIndex(r => r.BookingId)
            .IsUnique()
            .HasFilter("is_deleted = FALSE")
            .HasDatabaseName("uq_reviews_booking");
        builder.HasIndex(r => r.ResourceId)
            .HasDatabaseName("ix_reviews_resource");
        builder.HasIndex(r => r.CustomerId)
            .HasDatabaseName("ix_reviews_customer");
    }
}
