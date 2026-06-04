using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CompleteBookingCommand(Guid BookingId) : IRequest<Result>;

internal sealed class CompleteBookingCommandHandler(
    IBookingRepository bookingRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CompleteBookingCommand, Result>
{
    public async Task<Result> Handle(CompleteBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure(BookingErrors.NotFound);

        booking.Complete();
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
