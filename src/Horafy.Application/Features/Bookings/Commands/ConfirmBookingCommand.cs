using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record ConfirmBookingCommand(Guid BookingId) : IRequest<Result>;

internal sealed class ConfirmBookingCommandHandler(
    IBookingRepository bookingRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<ConfirmBookingCommand, Result>
{
    public async Task<Result> Handle(
        ConfirmBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure(BookingErrors.NotFound);

        booking.Confirm();
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
