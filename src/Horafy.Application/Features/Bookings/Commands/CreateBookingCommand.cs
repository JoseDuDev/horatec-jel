using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CreateBookingCommand(
    Guid ServiceId,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.ScheduledAt)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("O horário deve ser futuro.");
    }
}

internal sealed class CreateBookingCommandHandler(
    IServiceRepository serviceRepository,
    IResourceRepository resourceRepository,
    IBookingRepository bookingRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateBookingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateBookingCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized);

        var service = await serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken);
        if (service is null) return Result.Failure<Guid>(BookingErrors.ServiceNotFound);

        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure<Guid>(BookingErrors.ResourceNotFound);

        var endsAt = request.ScheduledAt.AddMinutes(service.DurationMinutes);
        var hasConflict = await bookingRepository.HasConflictAsync(
            request.ResourceId, request.ScheduledAt, endsAt,
            cancellationToken: cancellationToken);

        if (hasConflict) return Result.Failure<Guid>(BookingErrors.Conflict);

        var booking = Booking.Create(
            request.ServiceId,
            request.ResourceId,
            customerId:      currentUser.UserId.Value,
            customerName:    currentUser.Email ?? "Cliente",
            customerEmail:   currentUser.Email ?? string.Empty,
            scheduledAt:     request.ScheduledAt,
            durationMinutes: service.DurationMinutes,
            notes: request.Notes);

        bookingRepository.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(booking.Id);
    }
}
