using FluentAssertions;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class ConfirmPaymentCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepo = new();
    private readonly Mock<IPaymentGateway>    _gateway     = new();
    private readonly Mock<ITenantUnitOfWork>  _unitOfWork  = new();

    private ConfirmPaymentCommandHandler MakeHandler() =>
        new(_paymentRepo.Object, _gateway.Object, _unitOfWork.Object);

    private static Payment MakePendingPayment(Guid bookingId) =>
        Payment.Create(bookingId, "pref_123", PaymentMethod.Pix, 100m, 0m);

    [Fact]
    public async Task Handle_PendingPayment_ApprovesAndReturnsSuccess()
    {
        var bookingId = Guid.NewGuid();
        var payment   = MakePendingPayment(bookingId);

        _paymentRepo.Setup(r => r.GetByMpPaymentIdAsync("mp_999", default))
            .ReturnsAsync((Payment?)null);
        _paymentRepo.Setup(r => r.GetByPreferenceIdAsync("pref_123", default))
            .ReturnsAsync(payment);
        _gateway.Setup(g => g.GetPaymentStatusAsync("mp_999", default))
            .ReturnsAsync(new PaymentStatusResult("mp_999", "pref_123", PaymentStatus.Approved, DateTimeOffset.UtcNow));

        var result = await MakeHandler().Handle(new ConfirmPaymentCommand("mp_999"), default);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Approved);
    }

    [Fact]
    public async Task Handle_AlreadyApproved_ReturnsSuccessWithoutProcessing()
    {
        var payment = MakePendingPayment(Guid.NewGuid());
        payment.Approve("mp_999");

        _paymentRepo.Setup(r => r.GetByMpPaymentIdAsync("mp_999", default))
            .ReturnsAsync(payment);

        var result = await MakeHandler().Handle(new ConfirmPaymentCommand("mp_999"), default);

        result.IsSuccess.Should().BeTrue();
        _gateway.Verify(g => g.GetPaymentStatusAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_ReturnsNotFoundError()
    {
        _paymentRepo.Setup(r => r.GetByMpPaymentIdAsync("mp_999", default))
            .ReturnsAsync((Payment?)null);
        _gateway.Setup(g => g.GetPaymentStatusAsync("mp_999", default))
            .ReturnsAsync(new PaymentStatusResult("mp_999", "pref_unknown", PaymentStatus.Approved, null));
        _paymentRepo.Setup(r => r.GetByPreferenceIdAsync("pref_unknown", default))
            .ReturnsAsync((Payment?)null);

        var result = await MakeHandler().Handle(new ConfirmPaymentCommand("mp_999"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.NotFound");
    }
}
