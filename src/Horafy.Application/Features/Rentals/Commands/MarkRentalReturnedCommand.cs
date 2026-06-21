using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Commands;

/// <summary>Marca a devolução do item de uma locação (ação de admin/staff).</summary>
public sealed record MarkRentalReturnedCommand(Guid BookingId) : IRequest<Result<RentalReturnResult>>;

/// <summary>
/// Resultado da devolução. <see cref="LateFee"/> é informativo (calculado a partir da
/// diária do item); a cobrança depende da integração de pagamento (ver docs/rental-plan.md).
/// </summary>
public sealed record RentalReturnResult(Guid BookingId, int LateDays, decimal LateFee);

internal sealed class MarkRentalReturnedCommandHandler(
    IBookingRepository      bookingRepository,
    IRentableItemRepository rentableItemRepository,
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

        try
        {
            booking.MarkRentalReturned(returnedAt);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<RentalReturnResult>(RentalErrors.InvalidLifecycleTransition(ex.Message));
        }

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

        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RentalReturnResult(booking.Id, lateDays, lateFee));
    }
}
