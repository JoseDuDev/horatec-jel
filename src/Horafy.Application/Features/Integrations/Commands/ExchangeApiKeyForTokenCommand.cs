using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using Horafy.Shared.Security;
using MediatR;

namespace Horafy.Application.Features.Integrations.Commands;

/// <summary>
/// Troca uma API key válida por um JWT de serviço de curta duração.
/// Endpoint público (sem tenant resolvido): a chave identifica o tenant.
/// </summary>
public sealed record ExchangeApiKeyForTokenCommand(string ApiKey)
    : IRequest<Result<ServiceTokenResult>>;

public sealed record ServiceTokenResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string TokenType = "Bearer");

internal sealed class ExchangeApiKeyForTokenCommandHandler(
    IIntegrationApiKeyRepository repository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<ExchangeApiKeyForTokenCommand, Result<ServiceTokenResult>>
{
    public async Task<Result<ServiceTokenResult>> Handle(
        ExchangeApiKeyForTokenCommand request, CancellationToken cancellationToken)
    {
        var parsed = ApiKeyGenerator.Parse(request.ApiKey);
        if (parsed is null)
            return Result.Failure<ServiceTokenResult>(IntegrationErrors.InvalidApiKey);

        var apiKey = await repository.GetByPrefixAsync(parsed.Value.KeyPrefix, cancellationToken);
        if (apiKey is null || !apiKey.IsActive)
            return Result.Failure<ServiceTokenResult>(IntegrationErrors.InvalidApiKey);

        if (!ApiKeyGenerator.Verify(parsed.Value.Secret, apiKey.KeyHash))
            return Result.Failure<ServiceTokenResult>(IntegrationErrors.InvalidApiKey);

        apiKey.MarkUsed();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = tokenService.GenerateIntegrationToken(apiKey.TenantId, apiKey.Scopes);

        return Result.Success(new ServiceTokenResult(token.AccessToken, token.ExpiresAt));
    }
}
