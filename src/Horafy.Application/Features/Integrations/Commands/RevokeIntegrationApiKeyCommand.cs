using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Integrations.Commands;

public sealed record RevokeIntegrationApiKeyCommand(Guid Id) : IRequest<Result>;

internal sealed class RevokeIntegrationApiKeyCommandHandler(
    ICurrentUserService currentUser,
    IIntegrationApiKeyRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<RevokeIntegrationApiKeyCommand, Result>
{
    public async Task<Result> Handle(
        RevokeIntegrationApiKeyCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure(IntegrationErrors.TenantMissing);

        var apiKey = await repository.GetTrackedByIdAsync(request.Id, cancellationToken);

        // Não vaza existência de chaves de outros tenants.
        if (apiKey is null || apiKey.TenantId != tenantId.Value)
            return Result.Failure(IntegrationErrors.ApiKeyNotFound);

        apiKey.Revoke();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
