using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Integrations;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using Horafy.Shared.Security;
using MediatR;

namespace Horafy.Application.Features.Integrations.Commands;

public sealed record CreateIntegrationApiKeyCommand(
    string Name,
    string? Scopes) : IRequest<Result<CreatedApiKeyResult>>;

/// <summary>
/// Retornado UMA única vez na criação. <see cref="ApiKey"/> é o segredo em texto puro
/// e não pode ser recuperado depois.
/// </summary>
public sealed record CreatedApiKeyResult(
    Guid Id,
    string Name,
    string KeyPrefix,
    string ApiKey,
    string Scopes);

public sealed class CreateIntegrationApiKeyCommandValidator
    : AbstractValidator<CreateIntegrationApiKeyCommand>
{
    public CreateIntegrationApiKeyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Scopes).MaximumLength(500);
    }
}

internal sealed class CreateIntegrationApiKeyCommandHandler(
    ICurrentUserService currentUser,
    IIntegrationApiKeyRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateIntegrationApiKeyCommand, Result<CreatedApiKeyResult>>
{
    public async Task<Result<CreatedApiKeyResult>> Handle(
        CreateIntegrationApiKeyCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<CreatedApiKeyResult>(IntegrationErrors.TenantMissing);

        var (plainKey, keyPrefix, keyHash) = ApiKeyGenerator.Generate();

        var apiKey = IntegrationApiKey.Create(
            tenantId.Value, request.Name, keyPrefix, keyHash, request.Scopes);

        repository.Add(apiKey);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreatedApiKeyResult(
            apiKey.Id, apiKey.Name, keyPrefix, plainKey, apiKey.Scopes));
    }
}
