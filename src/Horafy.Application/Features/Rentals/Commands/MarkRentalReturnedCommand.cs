using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;

namespace Horafy.Application.Features.Rentals.Commands;

/// <summary>Para onde a caução foi estornada na devolução de uma locação.</summary>
public enum RentalRefundDestination
{
    /// <summary>Nada a estornar (caução zerada pela multa ou inexistente).</summary>
    None    = 0,

    /// <summary>Crédito na carteira do cliente.</summary>
    Wallet  = 1,

    /// <summary>Estorno (parcial) no meio de pagamento original — cartão/PIX.</summary>
    Gateway = 2,
}

/// <summary>
/// Marca a devolução do item de uma locação (ação de admin/staff).
/// Quando <see cref="RefundToGateway"/> é <c>true</c> e existe um pagamento aprovado no
/// gateway, a caução é estornada (parcialmente) no meio de pagamento original; caso
/// contrário — ou se o estorno no gateway falhar — cai para crédito na carteira, de modo
/// que o cliente nunca fique sem o estorno.
/// </summary>
public sealed record MarkRentalReturnedCommand(Guid BookingId, bool RefundToGateway = false)
    : IRequest<Result<RentalReturnResult>>;

/// <summary>
/// Resultado da devolução. A caução é estornada descontada da multa por atraso
/// (<see cref="LateFee"/>), nunca abaixo de zero, para o destino em <see cref="Destination"/>.
/// </summary>
public sealed record RentalReturnResult(
    Guid BookingId, int LateDays, decimal LateFee, decimal DepositRefunded,
    RentalRefundDestination Destination);

internal sealed class MarkRentalReturnedCommandHandler(
    IBookingRepository      bookingRepository,
    IRentableItemRepository rentableItemRepository,
    IWalletRepository       walletRepository,
    IPaymentRepository      paymentRepository,
    IPaymentGateway         paymentGateway,
    ITenantUnitOfWork       unitOfWork) : IRequestHandler<MarkRentalReturnedCommand, Result<RentalReturnResult>>
{
    public async Task<Result<RentalReturnResult>> Handle(
        MarkRentalReturnedCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null)
            return Result.Failure<RentalReturnResult>(RentalErrors.BookingNotFound);
        if (booking.Kind != BookingKind.Rental)
            return Result.Failure<RentalReturnResult>(RentalErrors.NotARental);

        var returnedAt = DateTimeOffset.UtcNow;

        var lateDays = RentalPricing.LateDays(
            DateOnly.FromDateTime(booking.EndsAt.UtcDateTime),
            DateOnly.FromDateTime(returnedAt.UtcDateTime));

        var lateFee = 0m;
        if (lateDays > 0)
        {
            var itemIds = booking.Services.Where(s => s.RentableItemId.HasValue)
                .Select(s => s.RentableItemId!.Value).Distinct();
            var items = (await rentableItemRepository.GetByIdsAsync(itemIds, cancellationToken))
                .ToDictionary(i => i.Id);

            foreach (var line in booking.Services.Where(s => s.RentableItemId.HasValue))
                if (items.TryGetValue(line.RentableItemId!.Value, out var item))
                    lateFee += RentalPricing.CalculateLateFee(item.DailyRate, lateDays, line.Quantity);
        }

        // Estorna a caução descontada a multa, nunca < 0.
        var refund = Math.Max(0m, booking.SecurityDeposit - lateFee);

        try
        {
            booking.MarkRentalReturned(returnedAt, lateFee, refund);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<RentalReturnResult>(RentalErrors.InvalidLifecycleTransition(ex.Message));
        }

        var destination = refund > 0
            ? await RefundDepositAsync(booking, refund, request.RefundToGateway, cancellationToken)
            : RentalRefundDestination.None;

        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new RentalReturnResult(booking.Id, lateDays, lateFee, refund, destination));
    }

    private async Task<RentalRefundDestination> RefundDepositAsync(
        Booking booking, decimal refund, bool preferGateway, CancellationToken ct)
    {
        // Estorno PARCIAL no meio de pagamento original: apenas a caução é devolvida —
        // a diária é receita e permanece. Por isso o Payment NÃO é marcado como Refunded
        // (não há estado "parcialmente estornado"); o ledger da caução é booking.DepositRefunded.
        // Só se aplica a pagamentos aprovados no gateway; qualquer falha cai para a carteira.
        if (preferGateway)
        {
            var payment = await paymentRepository.GetByBookingIdAsync(booking.Id, ct);
            if (payment is { Status: PaymentStatus.Approved, MpPaymentId: { } mpId })
            {
                var result = await paymentGateway.RefundAsync(mpId, refund, ct);
                if (result.Success)
                    return RentalRefundDestination.Gateway;
            }
        }

        // Carteira — padrão e fallback do gateway.
        var wallet = await walletRepository.GetByUserIdAsync(booking.CustomerId, ct);
        var isNew  = wallet is null;
        wallet ??= WalletEntity.Create(booking.CustomerId);

        wallet.RefundFromBooking(
            refund, $"Estorno de caução — locação #{booking.Id.ToString()[..8]}", booking.Id);

        if (isNew) walletRepository.Add(wallet);
        else       walletRepository.Update(wallet);

        return RentalRefundDestination.Wallet;
    }
}
