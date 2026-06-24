using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Application.Features.Tenants;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record CreateResourceCommand(
    string Name,
    ResourceType Type,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    string? AvatarUrl,
    Guid? UserId) : IRequest<Result<Guid>>;

public sealed class CreateResourceCommandValidator : AbstractValidator<CreateResourceCommand>
{
    public CreateResourceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
    }
}

internal sealed class CreateResourceCommandHandler(
    IResourceRepository resourceRepository,
    ITenantPlanService tenantPlan,
    IPlanLimitsService planLimits,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateResourceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateResourceCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantPlan.GetCurrentTenantAsync(cancellationToken);
        if (tenant is not null)
        {
            if (!tenant.Has(TenantCapability.Appointments))
                return Result.Failure<Guid>(PlanErrors.AppointmentsNotEnabled);

            var limits = await planLimits.GetLimitsAsync(tenant.Plan, cancellationToken);
            var count  = await resourceRepository.CountAsync(cancellationToken: cancellationToken);
            if (limits.ResourcesReached(count))
                return Result.Failure<Guid>(PlanErrors.ResourceLimitReached(limits.MaxResources));
        }

        var resource = Resource.Create(
            request.Name, request.Type, request.Email, request.Phone,
            request.Specialty, request.Bio, request.AvatarUrl, request.UserId);

        resourceRepository.Add(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(resource.Id);
    }
}
