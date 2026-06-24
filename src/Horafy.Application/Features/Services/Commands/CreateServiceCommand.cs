using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Application.Features.Tenants;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Services.Commands;

public sealed record CreateServiceCommand(
    string Name,
    int DurationMinutes,
    decimal Price,
    string? Description,
    string? Category) : IRequest<Result<Guid>>;

public sealed class CreateServiceCommandValidator : AbstractValidator<CreateServiceCommand>
{
    public CreateServiceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateServiceCommandHandler(
    IServiceRepository serviceRepository,
    ITenantPlanService tenantPlan,
    IPlanLimitsService planLimits,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateServiceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateServiceCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantPlan.GetCurrentTenantAsync(cancellationToken);
        if (tenant is not null)
        {
            if (!tenant.Has(TenantCapability.Appointments))
                return Result.Failure<Guid>(PlanErrors.AppointmentsNotEnabled);

            var limits = await planLimits.GetLimitsAsync(tenant.Plan, cancellationToken);
            var count  = await serviceRepository.CountAsync(cancellationToken: cancellationToken);
            if (limits.ServicesReached(count))
                return Result.Failure<Guid>(PlanErrors.ServiceLimitReached(limits.MaxServices));
        }

        if (await serviceRepository.ExistsByNameAsync(request.Name, cancellationToken))
            return Result.Failure<Guid>(ServiceErrors.NameAlreadyExists);

        var service = Service.Create(
            request.Name, request.DurationMinutes, request.Price,
            request.Description, request.Category);

        serviceRepository.Add(service);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(service.Id);
    }
}
