using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Queries;

public sealed record GetFinancialSummaryQuery(DateTimeOffset From, DateTimeOffset To)
    : IRequest<Result<FinancialSummaryResult>>;

public sealed record FinancialSummaryResult(
    decimal TotalRevenue, decimal TotalRefunded, decimal NetRevenue,
    IReadOnlyList<DailySummary> ByDay);

public sealed record DailySummary(DateOnly Date, decimal Revenue, int Count);

internal sealed class GetFinancialSummaryQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetFinancialSummaryQuery, Result<FinancialSummaryResult>>
{
    public async Task<Result<FinancialSummaryResult>> Handle(
        GetFinancialSummaryQuery request, CancellationToken cancellationToken)
    {
        var payments = await paymentRepository.GetByPeriodAsync(request.From, request.To, cancellationToken);

        var approved = payments.Where(p => p.Status == PaymentStatus.Approved).ToList();
        var refunded = payments.Where(p => p.Status == PaymentStatus.Refunded).ToList();

        var totalRevenue  = approved.Sum(p => p.Amount);
        var totalRefunded = refunded.Sum(p => p.Amount);

        var byDay = approved
            .GroupBy(p => DateOnly.FromDateTime(p.PaidAt?.DateTime ?? p.CreatedAt.DateTime))
            .Select(g => new DailySummary(g.Key, g.Sum(p => p.Amount), g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        return Result.Success(new FinancialSummaryResult(
            totalRevenue, totalRefunded, totalRevenue - totalRefunded, byDay));
    }
}
