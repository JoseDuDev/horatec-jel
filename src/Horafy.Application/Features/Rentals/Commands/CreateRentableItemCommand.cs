using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Commands;

public sealed record CreateRentableItemCommand(
    string  Name,
    int     Quantity,
    decimal DailyRate,
    decimal SecurityDeposit,
    int     BufferDays,
    string? Description,
    string? Category,
    string? ImageUrl) : IRequest<Result<Guid>>;

public sealed class CreateRentableItemCommandValidator : AbstractValidator<CreateRentableItemCommand>
{
    public CreateRentableItemCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.DailyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SecurityDeposit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BufferDays).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateRentableItemCommandHandler(
    IRentableItemRepository rentableItemRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateRentableItemCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateRentableItemCommand request, CancellationToken cancellationToken)
    {
        var item = RentableItem.Create(
            request.Name, request.Quantity, request.DailyRate,
            request.SecurityDeposit, request.BufferDays,
            request.Description, request.Category, request.ImageUrl);

        rentableItemRepository.Add(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(item.Id);
    }
}
