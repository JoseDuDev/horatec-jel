using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Queries;

public sealed record GetFinancialReportQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? ServiceId,
    Guid? ResourceId) : IRequest<Result<IReadOnlyList<PaymentTransactionResult>>>;

public sealed record PaymentTransactionResult(
    Guid Id, Guid BookingId, PaymentMethod Method, PaymentStatus Status,
    decimal Amount, decimal DepositAmount, DateTimeOffset? PaidAt, DateTimeOffset CreatedAt);

internal sealed class GetFinancialReportQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetFinancialReportQuery, Result<IReadOnlyList<PaymentTransactionResult>>>
{
    public async Task<Result<IReadOnlyList<PaymentTransactionResult>>> Handle(
        GetFinancialReportQuery request, CancellationToken cancellationToken)
    {
        var payments = await paymentRepository.GetByPeriodAsync(request.From, request.To, cancellationToken);

        var results = payments
            .Select(p => new PaymentTransactionResult(
                p.Id, p.BookingId, p.Method, p.Status,
                p.Amount, p.DepositAmount, p.PaidAt, p.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<PaymentTransactionResult>>(results);
    }
}
