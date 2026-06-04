using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CancelBookingCommand(Guid BookingId, string? Reason) : IRequest<Result>;

internal sealed class CancelBookingCommandHandler(
    IBookingRepository bookingRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CancelBookingCommand, Result>
{
    public async Task<Result> Handle(
        CancelBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure(BookingErrors.NotFound);

        // Apenas o próprio cliente ou staff/admin pode cancelar
        var isOwner = booking.CustomerId == currentUser.UserId;
        var isStaff = currentUser.Role is UserRole.TenantOwner or UserRole.TenantAdmin or UserRole.TenantStaff or UserRole.PlatformAdmin;

        if (!isOwner && !isStaff)
            return Result.Failure(BookingErrors.NotOwner);

        booking.Cancel(request.Reason);
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
