using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Integrations.Commands;

/// <summary>Desativa o webhook do tenant (mantém a config, para de entregar).</summary>
public sealed record DeleteIntegrationWebhookCommand() : IRequest<Result>;

internal sealed class DeleteIntegrationWebhookCommandHandler(
    ICurrentUserService currentUser,
    IIntegrationWebhookRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteIntegrationWebhookCommand, Result>
{
    public async Task<Result> Handle(
        DeleteIntegrationWebhookCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure(IntegrationErrors.TenantMissing);

        var webhook = await repository.GetByTenantAsync(tenantId.Value, cancellationToken);
        if (webhook is null)
            return Result.Failure(IntegrationErrors.WebhookNotConfigured);

        webhook.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
