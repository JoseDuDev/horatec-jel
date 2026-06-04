using FluentAssertions;
using Horafy.Application.Features.Payments.Queries;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Payments;

public sealed class GetFinancialReportQueryHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepo = new();

    private GetFinancialReportQueryHandler MakeHandler() =>
        new(_paymentRepo.Object);

    [Fact]
    public async Task Handle_ReturnsPaymentsInPeriod()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to   = DateTimeOffset.UtcNow;

        var payments = new List<Payment>
        {
            Payment.Create(Guid.NewGuid(), "pref_1", PaymentMethod.Pix, 100m, 0m),
            Payment.Create(Guid.NewGuid(), "pref_2", PaymentMethod.CreditCard, 200m, 0m)
        };
        payments[0].Approve("mp_1");

        _paymentRepo.Setup(r => r.GetByPeriodAsync(from, to, default)).ReturnsAsync(payments);

        var result = await MakeHandler().Handle(
            new GetFinancialReportQuery(from, to, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_EmptyPeriod_ReturnsEmptyList()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to   = DateTimeOffset.UtcNow;

        _paymentRepo.Setup(r => r.GetByPeriodAsync(from, to, default))
            .ReturnsAsync(new List<Payment>());

        var result = await MakeHandler().Handle(
            new GetFinancialReportQuery(from, to, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
