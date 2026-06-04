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
    ITenantUnitOfWork unitOfWork,
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway) : IRequestHandler<CancelBookingCommand, Result>
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

        // Cancellation fee if outside allowed period and there's an approved payment
        if (currentTenant.TenantId.HasValue)
        {
            var tenantForFee = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenantForFee?.CancellationPolicy.CancellationFeePercent > 0
                && !tenantForFee.CancellationPolicy.CanCancelAt(booking.ScheduledAt, DateTimeOffset.UtcNow))
            {
                var payment = await paymentRepository.GetByBookingIdAsync(booking.Id, cancellationToken);
                if (payment?.Status == Domain.Entities.Payments.PaymentStatus.Approved && payment.MpPaymentId is not null)
                {
                    var feeAmount  = Math.Round(payment.Amount * tenantForFee.CancellationPolicy.CancellationFeePercent / 100, 2);
                    var netRefund  = payment.Amount - feeAmount;
                    var refundResult = await paymentGateway.RefundAsync(payment.MpPaymentId, netRefund, cancellationToken);
                    if (refundResult.Success)
                    {
                        payment.Refund();
                        paymentRepository.Update(payment);
                        booking.MarkPaymentRefunded();
                    }
                }
            }
        }

        booking.Cancel(request.Reason);
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
