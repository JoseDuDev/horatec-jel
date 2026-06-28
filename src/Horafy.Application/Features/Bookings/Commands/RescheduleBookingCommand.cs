using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record RescheduleBookingCommand(
    Guid           BookingId,
    DateTimeOffset NewScheduledAt) : IRequest<Result>;

public sealed class RescheduleBookingCommandValidator : AbstractValidator<RescheduleBookingCommand>
{
    public RescheduleBookingCommandValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.NewScheduledAt)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("O novo horário deve ser futuro.");
    }
}

internal sealed class RescheduleBookingCommandHandler(
    IBookingRepository  bookingRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork   unitOfWork)
    : IRequestHandler<RescheduleBookingCommand, Result>
{
    public async Task<Result> Handle(RescheduleBookingCommand request, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, ct);
        if (booking is null)
            return Result.Failure(BookingErrors.NotFound);

        var isStaff = currentUser.Role is UserRole.TenantOwner or UserRole.TenantAdmin
                      or UserRole.TenantStaff or UserRole.PlatformAdmin;

        if (!isStaff && booking.CustomerId != currentUser.UserId)
            return Result.Failure(BookingErrors.NotOwner);

        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Completed or BookingStatus.NoShow)
            return Result.Failure(BookingErrors.NotReschedulable);

        if (booking.ResourceId.HasValue)
        {
            var endsAt = request.NewScheduledAt.AddMinutes(booking.DurationMinutes);
            var hasConflict = await bookingRepository.HasConflictAsync(
                booking.ResourceId.Value,
                request.NewScheduledAt,
                endsAt,
                excludeBookingId: booking.Id,
                ct);

            if (hasConflict)
                return Result.Failure(BookingErrors.Conflict);
        }

        booking.Reschedule(request.NewScheduledAt);
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
