using FluentAssertions;
using Horafy.Domain.Entities.Bookings;
using Xunit;

namespace Horafy.Domain.Tests.Bookings;

public sealed class BookingKindTests
{
    [Fact]
    public void Create_DefaultsToAppointmentKind()
    {
        // Garante compatibilidade: reservas existentes/novas continuam como agendamento.
        var booking = Booking.Create(
            services: new[] { (Guid.NewGuid(), "Corte", 30, 50m) },
            resourceId: Guid.NewGuid(),
            resourceName: "Recurso",
            customerId: Guid.NewGuid(),
            customerName: "João",
            customerEmail: "joao@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2));

        booking.Kind.Should().Be(BookingKind.Appointment);
    }
}
