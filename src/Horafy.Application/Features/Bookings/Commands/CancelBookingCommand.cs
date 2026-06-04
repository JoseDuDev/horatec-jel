using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CancelBookingCommand(Guid BookingId, string? Reason) : IRequest<Result>;

internal sealed class CancelBookingCommandHandler(
    IBookingRepository bookingRepository,
    ITenantRepository tenantRepository,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CancelBookingCommand, Result>
{
    public async Task<Result> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure(BookingErrors.NotFound);

        var isOwner = booking.CustomerId == currentUser.UserId;
        var isStaff = currentUser.Role is
            UserRole.TenantOwner or UserRole.TenantAdmin or
            UserRole.TenantStaff or UserRole.PlatformAdmin;

        if (!isOwner && !isStaff) return Result.Failure(BookingErrors.NotOwner);

        if (isOwner && !isStaff && currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null)
            {
                if (!tenant.CancellationPolicy.AllowCustomerCancellation)
                    return Result.Failure(BookingErrors.CancellationNotAllowed);

                if (!tenant.CancellationPolicy.CanCancelAt(booking.ScheduledAt, DateTimeOffset.UtcNow))
                    return Result.Failure(BookingErrors.CancellationWindowClosed);
            }
        }

        booking.Cancel(request.Reason);
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
