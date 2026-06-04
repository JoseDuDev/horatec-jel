using FluentAssertions;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class CreatePaymentCommandHandlerTests
{
    private readonly Mock<IBookingRepository>    _bookingRepo = new();
    private readonly Mock<ITenantRepository>     _tenantRepo  = new();
    private readonly Mock<IPaymentRepository>    _paymentRepo = new();
    private readonly Mock<IPaymentGateway>       _gateway     = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc   = new();
    private readonly Mock<ITenantUnitOfWork>     _unitOfWork  = new();

    private CreatePaymentCommandHandler MakeHandler() =>
        new(_bookingRepo.Object, _tenantRepo.Object, _paymentRepo.Object,
            _gateway.Object, _tenantSvc.Object, _unitOfWork.Object);

    private static Booking MakeBooking()
    {
        var service  = Service.Create("Corte", 60, 100m);
        var resource = Resource.Create("João", ResourceType.Professional);
        return Booking.Create(
            new[] { (service.Id, "Corte", 60) },
            resource.Id,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2));
    }

    private static Tenant MakeTenant() =>
        Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);

    [Fact]
    public async Task Handle_ValidRequest_CreatesPaymentAndReturnsPreferenceId()
    {
        var booking  = MakeBooking();
        var tenantId = Guid.NewGuid();
        var tenant   = MakeTenant();

        _tenantSvc.SetupGet(t => t.TenantId).Returns(tenantId);
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);
        _gateway.Setup(g => g.CreatePreferenceAsync(It.IsAny<CreatePaymentPreferenceRequest>(), default))
            .ReturnsAsync(new PaymentPreferenceResult("pref_abc", "https://mp.com/pref_abc", null));

        var result = await MakeHandler().Handle(
            new CreatePaymentCommand(booking.Id, 100m, PaymentMethod.Pix, "https://app.com/return"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.PreferenceId.Should().Be("pref_abc");
    }

    [Fact]
    public async Task Handle_BookingNotFound_ReturnsNotFoundError()
    {
        _tenantSvc.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Booking?)null);

        var result = await MakeHandler().Handle(
            new CreatePaymentCommand(Guid.NewGuid(), 100m, PaymentMethod.Pix, "https://app.com"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.NotFound");
    }

    [Fact]
    public async Task Handle_GatewayThrows_PropagatesException()
    {
        var booking  = MakeBooking();
        var tenantId = Guid.NewGuid();
        var tenant   = MakeTenant();

        _tenantSvc.SetupGet(t => t.TenantId).Returns(tenantId);
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);
        _gateway.Setup(g => g.CreatePreferenceAsync(It.IsAny<CreatePaymentPreferenceRequest>(), default))
            .ThrowsAsync(new HttpRequestException("MP offline"));

        var act = async () => await MakeHandler().Handle(
            new CreatePaymentCommand(booking.Id, 100m, PaymentMethod.Pix, "https://app.com"), default);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
