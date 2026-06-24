using FluentValidation;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdatePlanConfig;

/// <summary>
/// Define os limites de um plano (ação da plataforma). Faz upsert na tabela
/// plan_configurations, sobrepondo os defaults de <see cref="PlanLimits.For"/>.
/// </summary>
public sealed record UpdatePlanConfigCommand(
    TenantPlan Plan,
    int MaxServices,
    int MaxResources,
    int MaxRentableItems) : IRequest<Result>;

public sealed class UpdatePlanConfigCommandValidator : AbstractValidator<UpdatePlanConfigCommand>
{
    public UpdatePlanConfigCommandValidator()
    {
        // -1 = ilimitado; abaixo disso é inválido.
        RuleFor(x => x.MaxServices).GreaterThanOrEqualTo(-1);
        RuleFor(x => x.MaxResources).GreaterThanOrEqualTo(-1);
        RuleFor(x => x.MaxRentableItems).GreaterThanOrEqualTo(-1);
    }
}

internal sealed class UpdatePlanConfigCommandHandler(
    IPlanConfigurationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdatePlanConfigCommand, Result>
{
    public async Task<Result> Handle(UpdatePlanConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await repository.GetByPlanAsync(request.Plan, cancellationToken);
        if (config is null)
        {
            config = PlanConfiguration.Create(
                request.Plan, request.MaxServices, request.MaxResources, request.MaxRentableItems);
            repository.Add(config);
        }
        else
        {
            config.Update(request.MaxServices, request.MaxResources, request.MaxRentableItems);
            repository.Update(config);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
