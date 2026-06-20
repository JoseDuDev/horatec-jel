using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CreateBookingCommand(
    IReadOnlyList<Guid> ServiceIds,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.ServiceIds).NotEmpty().WithMessage("Pelo menos um serviço é obrigatório.");
        RuleForEach(x => x.ServiceIds).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.ScheduledAt).Must(d => d > DateTimeOffset.UtcNow)
            .WithMessage("O horário deve ser futuro.");
    }
}

internal sealed class CreateBookingCommandHandler(
    IServiceRepository serviceRepository,
    IResourceRepository resourceRepository,
    IBookingRepository bookingRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateBookingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateBookingCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized);

        var user = await userRepository.GetByIdAsync(currentUser.UserId.Value, cancellationToken);
        var customerPhone = user?.Phone;

        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure<Guid>(BookingErrors.ResourceNotFound);

        var fetchedServices = await serviceRepository.GetByIdsAsync(request.ServiceIds, cancellationToken);
        var serviceMap = fetchedServices.ToDictionary(s => s.Id);

        var services = new List<(Guid ServiceId, string ServiceName, int DurationMinutes, decimal Price)>();
        foreach (var serviceId in request.ServiceIds)
        {
            if (!serviceMap.TryGetValue(serviceId, out var service))
                return Result.Failure<Guid>(BookingErrors.ServiceNotFound);
            services.Add((service.Id, service.Name, service.DurationMinutes, service.Price));
        }

        var totalDuration = services.Sum(s => s.DurationMinutes);
        var endsAt = request.ScheduledAt.AddMinutes(totalDuration);

        var hasConflict = await bookingRepository.HasConflictAsync(
            request.ResourceId, request.ScheduledAt, endsAt,
            cancellationToken: cancellationToken);

        if (hasConflict) return Result.Failure<Guid>(BookingErrors.Conflict);

        var booking = Booking.Create(
            services,
            request.ResourceId,
            resource.Name,
            customerId:    currentUser.UserId.Value,
            customerName:  currentUser.Email ?? "Cliente",
            customerEmail: currentUser.Email ?? string.Empty,
            scheduledAt:   request.ScheduledAt,
            customerPhone: customerPhone,
            notes:         request.Notes);

        bookingRepository.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(booking.Id);
    }
}
