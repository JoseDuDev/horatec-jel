using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;
using System.Data;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record CreateRecurringBookingCommand(
    Guid ServiceId,
    Guid ResourceId,
    DateTimeOffset FirstOccurrence,
    RecurrenceFrequency Frequency,
    int OccurrenceCount,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class CreateRecurringBookingCommandValidator
    : AbstractValidator<CreateRecurringBookingCommand>
{
    public CreateRecurringBookingCommandValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.FirstOccurrence).Must(d => d > DateTimeOffset.UtcNow)
            .WithMessage("A primeira ocorrência deve ser futura.");
        RuleFor(x => x.OccurrenceCount).InclusiveBetween(2, 52)
            .WithMessage("OccurrenceCount deve ser entre 2 e 52.");
    }
}

internal sealed class CreateRecurringBookingCommandHandler(
    IServiceRepository serviceRepository,
    IResourceRepository resourceRepository,
    IBookingRepository bookingRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateRecurringBookingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateRecurringBookingCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized);

        var service = await serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken);
        if (service is null) return Result.Failure<Guid>(BookingErrors.ServiceNotFound);

        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure<Guid>(BookingErrors.ResourceNotFound);

        var occurrences = GenerateOccurrences(request.FirstOccurrence, request.Frequency, request.OccurrenceCount);

        foreach (var date in occurrences)
        {
            var end = date.AddMinutes(service.DurationMinutes);
            var hasConflict = await bookingRepository.HasConflictAsync(
                request.ResourceId, date, end, cancellationToken: cancellationToken);
            if (hasConflict) return Result.Failure<Guid>(BookingErrors.Conflict);
        }

        var recurrenceGroupId = Guid.NewGuid();

        await using var tx = await unitOfWork.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        foreach (var date in occurrences)
        {
            var booking = Booking.Create(
                request.ServiceId,
                request.ResourceId,
                customerId:        currentUser.UserId.Value,
                customerName:      currentUser.Email ?? "Cliente",
                customerEmail:     currentUser.Email ?? string.Empty,
                scheduledAt:       date,
                durationMinutes:   service.DurationMinutes,
                notes:             request.Notes,
                recurrenceGroupId: recurrenceGroupId);

            bookingRepository.Add(booking);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Result.Success(recurrenceGroupId);
    }

    private static IReadOnlyList<DateTimeOffset> GenerateOccurrences(
        DateTimeOffset first, RecurrenceFrequency frequency, int count)
    {
        var dates = new List<DateTimeOffset> { first };
        for (var i = 1; i < count; i++)
        {
            var prev = dates[^1];
            dates.Add(frequency switch
            {
                RecurrenceFrequency.Weekly   => prev.AddDays(7),
                RecurrenceFrequency.Biweekly => prev.AddDays(14),
                RecurrenceFrequency.Monthly  => prev.AddMonths(1),
                _ => throw new ArgumentOutOfRangeException(nameof(frequency))
            });
        }
        return dates;
    }
}
