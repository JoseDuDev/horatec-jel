using Horafy.Domain.Entities.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class BookingServiceEntityConfiguration : IEntityTypeConfiguration<BookingService>
{
    public void Configure(EntityTypeBuilder<BookingService> builder)
    {
        builder.ToTable("booking_services");

        builder.HasKey(bs => bs.Id);

        builder.Property(bs => bs.Id).ValueGeneratedNever();

        builder.Property(bs => bs.ServiceName).IsRequired().HasMaxLength(200);

        builder.HasIndex(bs => bs.BookingId)
            .HasDatabaseName("ix_booking_services_booking");
    }
}
