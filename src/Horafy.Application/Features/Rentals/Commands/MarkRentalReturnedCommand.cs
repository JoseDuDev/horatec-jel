using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;

namespace Horafy.Application.Features.Rentals.Commands;

/// <summary>Marca a devolução do item de uma locação (ação de admin/staff).</summary>
public sealed record MarkRentalReturnedCommand(Guid BookingId) : IRequest<Result<RentalReturnResult>>;

/// <summary>
/// Resultado da devolução. A caução é estornada para a carteira do cliente descontada
/// da multa por atraso (<see cref="LateFee"/>), nunca abaixo de zero.
/// </summary>
public sealed record RentalReturnResult(Guid BookingId, int LateDays, decimal LateFee, decimal DepositRefunded);

internal sealed class MarkRentalReturnedCommandHandler(
    IBookingRepository      bookingRepository,
    IRentableItemRepository rentableItemRepository,
    IWalletRepository       walletRepository,
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

        // Estorna a caução (descontada a multa, nunca < 0) como crédito na carteira do cliente.
        var refund = Math.Max(0m, booking.SecurityDeposit - lateFee);

        try
        {
            booking.MarkRentalReturned(returnedAt, lateFee, refund);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<RentalReturnResult>(RentalErrors.InvalidLifecycleTransition(ex.Message));
        }

        if (refund > 0)
        {
            var wallet = await walletRepository.GetByUserIdAsync(booking.CustomerId, cancellationToken);
            var isNew  = wallet is null;
            wallet ??= WalletEntity.Create(booking.CustomerId);

            wallet.RefundFromBooking(
                refund, $"Estorno de caução — locação #{booking.Id.ToString()[..8]}", booking.Id);

            if (isNew) walletRepository.Add(wallet);
            else       walletRepository.Update(wallet);
        }

        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RentalReturnResult(booking.Id, lateDays, lateFee, refund));
    }
}
