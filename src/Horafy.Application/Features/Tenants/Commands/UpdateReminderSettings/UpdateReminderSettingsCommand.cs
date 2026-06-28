using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdateReminderSettings;

public sealed record UpdateReminderSettingsCommand(
    bool Enabled,
    int  FirstReminderHours,
    int  SecondReminderHours) : IRequest<Result>;

public sealed class UpdateReminderSettingsCommandValidator : AbstractValidator<UpdateReminderSettingsCommand>
{
    public UpdateReminderSettingsCommandValidator()
    {
        RuleFor(x => x.FirstReminderHours)
            .InclusiveBetween(0, 168);
        RuleFor(x => x.SecondReminderHours)
            .InclusiveBetween(0, 168);
        RuleFor(x => x.SecondReminderHours)
            .LessThan(x => x.FirstReminderHours)
            .When(x => x.FirstReminderHours > 0 && x.SecondReminderHours > 0)
            .WithMessage("O 2º lembrete deve ter antecedência menor que o 1º.");
    }
}

internal sealed class UpdateReminderSettingsCommandHandler(
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IUnitOfWork           unitOfWork) : IRequestHandler<UpdateReminderSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateReminderSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.UpdateReminderSettings(request.Enabled, request.FirstReminderHours, request.SecondReminderHours);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
