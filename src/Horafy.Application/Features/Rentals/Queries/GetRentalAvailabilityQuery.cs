using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Queries;

public sealed record GetRentalAvailabilityQuery(
    Guid ItemId,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<Result<RentalAvailabilityResult>>;

public sealed record RentalAvailabilityResult(
    Guid     ItemId,
    DateOnly StartDate,
    DateOnly EndDate,
    int      Days,
    int      TotalQuantity,
    int      ReservedUnits,
    int      AvailableUnits,
    bool     IsAvailable);

internal sealed class GetRentalAvailabilityQueryHandler(
    IRentableItemRepository rentableItemRepository,
    IBookingRepository      bookingRepository)
    : IRequestHandler<GetRentalAvailabilityQuery, Result<RentalAvailabilityResult>>
{
    public async Task<Result<RentalAvailabilityResult>> Handle(
        GetRentalAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (request.EndDate <= request.StartDate)
            return Result.Failure<RentalAvailabilityResult>(RentalErrors.InvalidPeriod);

        if (request.StartDate < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            return Result.Failure<RentalAvailabilityResult>(RentalErrors.PastDate);

        var item = await rentableItemRepository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item is null)
            return Result.Failure<RentalAvailabilityResult>(RentalErrors.ItemNotFound);

        if (!item.IsActive)
            return Result.Failure<RentalAvailabilityResult>(RentalErrors.ItemInactive);

        // Locação ocupa a janela [retirada 00:00, devolução 00:00) em UTC.
        var start = new DateTimeOffset(request.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var end   = new DateTimeOffset(request.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var reserved = await bookingRepository.CountReservedUnitsAsync(
            request.ItemId, start, end, item.BufferDays, cancellationToken: cancellationToken);

        var available = Math.Max(0, item.Quantity - reserved);
        var days      = request.EndDate.DayNumber - request.StartDate.DayNumber;

        return Result.Success(new RentalAvailabilityResult(
            ItemId:         item.Id,
            StartDate:      request.StartDate,
            EndDate:        request.EndDate,
            Days:           days,
            TotalQuantity:  item.Quantity,
            ReservedUnits:  reserved,
            AvailableUnits: available,
            IsAvailable:    available > 0));
    }
}
