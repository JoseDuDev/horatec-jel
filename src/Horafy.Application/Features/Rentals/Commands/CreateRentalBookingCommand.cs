using System.Data;
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Commands;

public sealed record RentalItemLine(Guid ItemId, int Quantity);

public sealed record CreateRentalBookingCommand(
    IReadOnlyList<RentalItemLine> Items,
    DateOnly StartDate,
    DateOnly EndDate,
    string?  Notes) : IRequest<Result<Guid>>;

public sealed class CreateRentalBookingCommandValidator : AbstractValidator<CreateRentalBookingCommand>
{
    public CreateRentalBookingCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("A devolução deve ser posterior à retirada.");
    }
}

internal sealed class CreateRentalBookingCommandHandler(
    IRentableItemRepository rentableItemRepository,
    IBookingRepository      bookingRepository,
    IUserRepository         userRepository,
    ICurrentUserService     currentUser,
    ITenantUnitOfWork       unitOfWork) : IRequestHandler<CreateRentalBookingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateRentalBookingCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized);

        if (request.EndDate <= request.StartDate)
            return Result.Failure<Guid>(RentalErrors.InvalidPeriod);

        if (request.StartDate < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            return Result.Failure<Guid>(RentalErrors.PastDate);

        var days  = RentalPricing.DaysBetween(request.StartDate, request.EndDate);
        var start = new DateTimeOffset(request.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var end   = new DateTimeOffset(request.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var user = await userRepository.GetByIdAsync(currentUser.UserId.Value, cancellationToken);

        // Transação Serializable (compatível com a retry-strategy do Npgsql): a verificação
        // de estoque e a inserção são atômicas. Reservas concorrentes do mesmo item conflitam
        // no commit (SSI do Postgres) e a operação é re-executada, impedindo overbooking.
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var lines = new List<(Guid RentableItemId, string ItemName, int Quantity, decimal LineTotal)>();
            var depositTotal = 0m;

            foreach (var line in request.Items)
            {
                var item = await rentableItemRepository.GetByIdAsync(line.ItemId, ct);
                if (item is null)
                    return Result.Failure<Guid>(RentalErrors.ItemNotFound);
                if (!item.IsActive)
                    return Result.Failure<Guid>(RentalErrors.ItemInactive);

                var reserved  = await bookingRepository.CountReservedUnitsAsync(
                    item.Id, start, end, item.BufferDays, cancellationToken: ct);
                var available = item.Quantity - reserved;

                if (line.Quantity > available)
                    return Result.Failure<Guid>(RentalErrors.OutOfStock);

                var quote = RentalPricing.Calculate(item.DailyRate, days, line.Quantity, item.SecurityDeposit);
                lines.Add((item.Id, item.Name, line.Quantity, quote.RentalAmount));
                depositTotal += quote.DepositAmount;
            }

            var booking = Booking.CreateRental(
                lines,
                customerId:      currentUser.UserId.Value,
                customerName:    currentUser.Email ?? "Cliente",
                customerEmail:   currentUser.Email ?? string.Empty,
                startsAt:        start,
                endsAt:          end,
                securityDeposit: depositTotal,
                customerPhone:   user?.Phone,
                notes:           request.Notes);

            bookingRepository.Add(booking);
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(booking.Id);
        }, IsolationLevel.Serializable, cancellationToken);
    }
}
