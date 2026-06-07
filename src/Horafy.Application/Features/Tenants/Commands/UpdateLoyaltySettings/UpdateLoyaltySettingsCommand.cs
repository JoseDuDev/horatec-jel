using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdateLoyaltySettings;

public sealed record UpdateLoyaltySettingsCommand(
    bool    IsEnabled,
    decimal CreditRatePercent,
    decimal MinBookingAmount) : IRequest<Result>;

public sealed class UpdateLoyaltySettingsCommandValidator : AbstractValidator<UpdateLoyaltySettingsCommand>
{
    public UpdateLoyaltySettingsCommandValidator()
    {
        RuleFor(x => x.CreditRatePercent)
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.MinBookingAmount)
            .GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateLoyaltySettingsCommandHandler(
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IUnitOfWork           unitOfWork) : IRequestHandler<UpdateLoyaltySettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateLoyaltySettingsCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.UpdateLoyaltySettings(request.IsEnabled, request.CreditRatePercent, request.MinBookingAmount);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
