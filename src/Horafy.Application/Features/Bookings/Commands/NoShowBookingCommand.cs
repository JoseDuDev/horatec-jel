using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record NoShowBookingCommand(Guid BookingId) : IRequest<Result>;

internal sealed class NoShowBookingCommandHandler(
    IBookingRepository bookingRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<NoShowBookingCommand, Result>
{
    public async Task<Result> Handle(NoShowBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure(BookingErrors.NotFound);

        booking.MarkNoShow();
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
