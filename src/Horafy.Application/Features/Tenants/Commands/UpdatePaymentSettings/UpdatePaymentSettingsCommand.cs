using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdatePaymentSettings;

public sealed record UpdatePaymentSettingsCommand(
    bool RequiresPayment,
    DepositMode DepositMode,
    decimal DepositValue) : IRequest<Result>;

public sealed class UpdatePaymentSettingsCommandValidator : AbstractValidator<UpdatePaymentSettingsCommand>
{
    public UpdatePaymentSettingsCommandValidator()
    {
        RuleFor(x => x.DepositValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DepositValue)
            .LessThanOrEqualTo(100)
            .When(x => x.DepositMode == DepositMode.Percentage);
    }
}

internal sealed class UpdatePaymentSettingsCommandHandler(
    ITenantRepository tenantRepository,
    ICurrentTenantService currentTenant,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdatePaymentSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdatePaymentSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.UpdatePaymentSettings(request.RequiresPayment, request.DepositMode, request.DepositValue);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
