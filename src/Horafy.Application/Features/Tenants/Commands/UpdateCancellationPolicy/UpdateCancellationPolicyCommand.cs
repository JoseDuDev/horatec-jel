using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdateCancellationPolicy;

public sealed record UpdateCancellationPolicyCommand(
    int     MinCancellationHours,
    decimal CancellationFeePercent,
    bool    AllowCustomerCancellation) : IRequest<Result>;

public sealed class UpdateCancellationPolicyCommandValidator : AbstractValidator<UpdateCancellationPolicyCommand>
{
    public UpdateCancellationPolicyCommandValidator()
    {
        RuleFor(x => x.MinCancellationHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CancellationFeePercent)
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    }
}

internal sealed class UpdateCancellationPolicyCommandHandler(
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IUnitOfWork           unitOfWork) : IRequestHandler<UpdateCancellationPolicyCommand, Result>
{
    public async Task<Result> Handle(UpdateCancellationPolicyCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.UpdateCancellationPolicy(
            request.MinCancellationHours,
            request.CancellationFeePercent,
            request.AllowCustomerCancellation);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
