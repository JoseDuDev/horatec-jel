using FluentAssertions;
using Horafy.Domain.Entities.Payments;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class PaymentAggregateTests
{
    private static Payment MakePending() =>
        Payment.Create(Guid.NewGuid(), "pref_abc123", PaymentMethod.Pix, 100m, 0m);

    [Fact]
    public void Create_ValidData_StatusIsPending()
    {
        var payment = MakePending();
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.MpPaymentId.Should().BeNull();
        payment.PaidAt.Should().BeNull();
    }

    [Fact]
    public void Approve_PendingPayment_SetsApprovedAndPaidAt()
    {
        var payment = MakePending();
        payment.Approve("mp_456");
        payment.Status.Should().Be(PaymentStatus.Approved);
        payment.MpPaymentId.Should().Be("mp_456");
        payment.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_AlreadyApproved_ThrowsInvalidOperation()
    {
        var payment = MakePending();
        payment.Approve("mp_1");
        var act = () => payment.Approve("mp_2");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_PendingPayment_SetsRejected()
    {
        var payment = MakePending();
        payment.Reject("mp_789");
        payment.Status.Should().Be(PaymentStatus.Rejected);
        payment.MpPaymentId.Should().Be("mp_789");
    }

    [Fact]
    public void Refund_ApprovedPayment_SetsRefunded()
    {
        var payment = MakePending();
        payment.Approve("mp_1");
        payment.Refund();
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void Refund_PendingPayment_ThrowsInvalidOperation()
    {
        var payment = MakePending();
        var act = () => payment.Refund();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_IsDeposit_RaisesPaymentConfirmedWithIsDepositTrue()
    {
        // DepositAmount < Amount → IsDeposit = true
        var payment = Payment.Create(Guid.NewGuid(), "pref_x", PaymentMethod.Pix, 100m, 30m);
        payment.Approve("mp_1");
        var evt = payment.DomainEvents.OfType<Horafy.Domain.Events.Payments.PaymentConfirmedEvent>().Single();
        evt.IsDeposit.Should().BeTrue();
    }

    [Fact]
    public void Approve_FullPayment_RaisesPaymentConfirmedWithIsDepositFalse()
    {
        var payment = Payment.Create(Guid.NewGuid(), "pref_x", PaymentMethod.Pix, 100m, 0m);
        payment.Approve("mp_1");
        var evt = payment.DomainEvents.OfType<Horafy.Domain.Events.Payments.PaymentConfirmedEvent>().Single();
        evt.IsDeposit.Should().BeFalse();
    }
}
