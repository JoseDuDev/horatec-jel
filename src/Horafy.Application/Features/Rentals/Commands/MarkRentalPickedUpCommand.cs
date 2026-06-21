using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Commands;

/// <summary>Marca a retirada do item de uma locação (ação de admin/staff).</summary>
public sealed record MarkRentalPickedUpCommand(Guid BookingId) : IRequest<Result>;

internal sealed class MarkRentalPickedUpCommandHandler(
    IBookingRepository bookingRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<MarkRentalPickedUpCommand, Result>
{
    public async Task<Result> Handle(
        MarkRentalPickedUpCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null)
            return Result.Failure(RentalErrors.BookingNotFound);
        if (booking.Kind != BookingKind.Rental)
            return Result.Failure(RentalErrors.NotARental);

        try
        {
            booking.MarkRentalPickedUp();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(RentalErrors.InvalidLifecycleTransition(ex.Message));
        }

        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
