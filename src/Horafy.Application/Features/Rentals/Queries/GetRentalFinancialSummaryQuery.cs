using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Queries;

/// <summary>
/// Resumo financeiro das locações criadas no período. A caução é um valor de
/// passagem (cobrada e estornada) — não é receita; a receita líquida são as
/// diárias mais as multas por atraso.
/// </summary>
public sealed record GetRentalFinancialSummaryQuery(DateTimeOffset From, DateTimeOffset To)
    : IRequest<Result<RentalFinancialSummaryResult>>;

public sealed record RentalFinancialSummaryResult(
    int     RentalCount,
    decimal RentalRevenue,      // diárias
    decimal LateFeesCollected,  // multas por atraso
    decimal DepositsCharged,    // caução cobrada
    decimal DepositsRefunded,   // caução estornada
    decimal DepositsHeld,       // caução ainda retida (cobrada − estornada)
    decimal NetRevenue);        // diárias + multas

internal sealed class GetRentalFinancialSummaryQueryHandler(IBookingRepository bookingRepository)
    : IRequestHandler<GetRentalFinancialSummaryQuery, Result<RentalFinancialSummaryResult>>
{
    public async Task<Result<RentalFinancialSummaryResult>> Handle(
        GetRentalFinancialSummaryQuery request, CancellationToken cancellationToken)
    {
        // Locações pagas (Confirmed/Completed) criadas no período.
        var rentals = await bookingRepository.FindAsync(
            b => b.Kind == BookingKind.Rental
              && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
              && b.CreatedAt >= request.From && b.CreatedAt <= request.To,
            cancellationToken);

        var rentalRevenue   = rentals.Sum(b => b.TotalAmount);
        var lateFees        = rentals.Sum(b => b.LateFee);
        var depositsCharged = rentals.Sum(b => b.SecurityDeposit);
        var depositsRefund  = rentals.Sum(b => b.DepositRefunded);

        return Result.Success(new RentalFinancialSummaryResult(
            RentalCount:       rentals.Count,
            RentalRevenue:     rentalRevenue,
            LateFeesCollected: lateFees,
            DepositsCharged:   depositsCharged,
            DepositsRefunded:  depositsRefund,
            DepositsHeld:      depositsCharged - depositsRefund,
            NetRevenue:        rentalRevenue + lateFees));
    }
}
