using FluentAssertions;
using Horafy.Application.Features.Payments.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class RefundPaymentCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepo = new();
    private readonly Mock<IPaymentGateway>    _gateway     = new();
    private readonly Mock<ITenantUnitOfWork>  _unitOfWork  = new();

    private RefundPaymentCommandHandler MakeHandler() =>
        new(_paymentRepo.Object, _gateway.Object, _unitOfWork.Object);

    private static Payment MakeApprovedPayment()
    {
        var p = Payment.Create(Guid.NewGuid(), "pref_x", PaymentMethod.Pix, 100m, 0m);
        p.Approve("mp_1");
        return p;
    }

    [Fact]
    public async Task Handle_ApprovedPayment_RefundsAndReturnsSuccess()
    {
        var payment = MakeApprovedPayment();
        _paymentRepo.Setup(r => r.GetByIdAsync(payment.Id, default)).ReturnsAsync(payment);
        _gateway.Setup(g => g.RefundAsync("mp_1", 100m, default))
            .ReturnsAsync(new RefundResult(true, null));

        var result = await MakeHandler().Handle(
            new RefundPaymentCommand(payment.Id, null), default);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_ReturnsNotFoundError()
    {
        _paymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Payment?)null);

        var result = await MakeHandler().Handle(
            new RefundPaymentCommand(Guid.NewGuid(), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.NotFound");
    }

    [Fact]
    public async Task Handle_NotApprovedPayment_ReturnsNotApprovedError()
    {
        var payment = Payment.Create(Guid.NewGuid(), "pref_x", PaymentMethod.Pix, 100m, 0m);
        _paymentRepo.Setup(r => r.GetByIdAsync(payment.Id, default)).ReturnsAsync(payment);

        var result = await MakeHandler().Handle(
            new RefundPaymentCommand(payment.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.NotApproved");
    }

    [Fact]
    public async Task Handle_GatewayRefundFails_ReturnsRefundFailedError()
    {
        var payment = MakeApprovedPayment();
        _paymentRepo.Setup(r => r.GetByIdAsync(payment.Id, default)).ReturnsAsync(payment);
        _gateway.Setup(g => g.RefundAsync("mp_1", 100m, default))
            .ReturnsAsync(new RefundResult(false, "MP error"));

        var result = await MakeHandler().Handle(
            new RefundPaymentCommand(payment.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.RefundFailed");
    }
}
