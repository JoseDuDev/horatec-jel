using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Queries;

public sealed record GetPaymentByBookingQuery(Guid BookingId) : IRequest<Result<PaymentResult?>>;

public sealed record PaymentResult(
    Guid Id, Guid BookingId, string PreferenceId, string? MpPaymentId,
    PaymentMethod Method, PaymentStatus Status, decimal Amount, decimal DepositAmount,
    string? PaymentUrl, DateTimeOffset? PaidAt, DateTimeOffset? ExpiresAt);

internal sealed class GetPaymentByBookingQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentByBookingQuery, Result<PaymentResult?>>
{
    public async Task<Result<PaymentResult?>> Handle(
        GetPaymentByBookingQuery request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByBookingIdAsync(request.BookingId, cancellationToken);
        if (payment is null) return Result.Success<PaymentResult?>(null);

        return Result.Success<PaymentResult?>(new PaymentResult(
            payment.Id, payment.BookingId, payment.PreferenceId, payment.MpPaymentId,
            payment.Method, payment.Status, payment.Amount, payment.DepositAmount,
            payment.PaymentUrl, payment.PaidAt, payment.ExpiresAt));
    }
}
