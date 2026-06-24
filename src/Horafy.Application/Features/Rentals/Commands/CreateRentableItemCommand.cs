using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Application.Features.Tenants;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Entities.Tenants;
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
    ITenantPlanService tenantPlan,
    IPlanLimitsService planLimits,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateRentableItemCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateRentableItemCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantPlan.GetCurrentTenantAsync(cancellationToken);
        if (tenant is not null)
        {
            if (!tenant.Has(TenantCapability.Rentals))
                return Result.Failure<Guid>(PlanErrors.RentalsNotEnabled);

            var limits = await planLimits.GetLimitsAsync(tenant.Plan, cancellationToken);
            var count  = await rentableItemRepository.CountAsync(cancellationToken: cancellationToken);
            if (limits.RentableItemsReached(count))
                return Result.Failure<Guid>(PlanErrors.RentableItemLimitReached(limits.MaxRentableItems));
        }

        var item = RentableItem.Create(
            request.Name, request.Quantity, request.DailyRate,
            request.SecurityDeposit, request.BufferDays,
            request.Description, request.Category, request.ImageUrl);

        rentableItemRepository.Add(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(item.Id);
    }
}
