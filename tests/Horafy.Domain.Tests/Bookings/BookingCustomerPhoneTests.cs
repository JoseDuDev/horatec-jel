using FluentAssertions;
using Horafy.Domain.Entities.Bookings;
using Xunit;

namespace Horafy.Domain.Tests.Bookings;

public sealed class BookingCustomerPhoneTests
{
    [Fact]
    public void Create_WithPhone_SetsCustomerPhone()
    {
        var booking = Booking.Create(
            services: new[] { (Guid.NewGuid(), "Corte", 30, 50m) },
            resourceId: Guid.NewGuid(),
            resourceName: "Recurso",
            customerId: Guid.NewGuid(),
            customerName: "João",
            customerEmail: "joao@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2),
            customerPhone: "+5511999998888");

        booking.CustomerPhone.Should().Be("+5511999998888");
    }

    [Fact]
    public void Create_WithoutPhone_CustomerPhoneIsNull()
    {
        var booking = Booking.Create(
            services: new[] { (Guid.NewGuid(), "Corte", 30, 50m) },
            resourceId: Guid.NewGuid(),
            resourceName: "Recurso",
            customerId: Guid.NewGuid(),
            customerName: "João",
            customerEmail: "joao@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2));

        booking.CustomerPhone.Should().BeNull();
    }
}
