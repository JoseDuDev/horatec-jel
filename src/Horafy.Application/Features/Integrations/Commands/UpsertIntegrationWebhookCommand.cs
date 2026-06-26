using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Integrations;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using Horafy.Shared.Security;
using MediatR;

namespace Horafy.Application.Features.Integrations.Commands;

/// <summary>
/// Cria ou atualiza o webhook de saída do tenant. Na criação (ou com
/// <see cref="RotateSecret"/>=true) o segredo é retornado UMA única vez.
/// </summary>
public sealed record UpsertIntegrationWebhookCommand(
    string Url,
    bool RotateSecret) : IRequest<Result<WebhookConfigResult>>;

/// <summary><see cref="Secret"/> só vem preenchido na criação ou rotação.</summary>
public sealed record WebhookConfigResult(string Url, string? Secret, bool IsActive);

public sealed class UpsertIntegrationWebhookCommandValidator
    : AbstractValidator<UpsertIntegrationWebhookCommand>
{
    public UpsertIntegrationWebhookCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().MaximumLength(500)
            .Must(BeHttpUrl).WithMessage("Use uma URL http(s) completa.");
    }

    private static bool BeHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

internal sealed class UpsertIntegrationWebhookCommandHandler(
    ICurrentUserService currentUser,
    IIntegrationWebhookRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertIntegrationWebhookCommand, Result<WebhookConfigResult>>
{
    public async Task<Result<WebhookConfigResult>> Handle(
        UpsertIntegrationWebhookCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<WebhookConfigResult>(IntegrationErrors.TenantMissing);

        var existing = await repository.GetByTenantAsync(tenantId.Value, cancellationToken);

        if (existing is null)
        {
            var secret = WebhookSignature.NewSecret();
            var webhook = IntegrationWebhook.Create(tenantId.Value, request.Url, secret);
            repository.Add(webhook);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(new WebhookConfigResult(webhook.Url, secret, webhook.IsActive));
        }

        existing.UpdateUrl(request.Url);
        existing.Activate();

        string? shownSecret = null;
        if (request.RotateSecret)
        {
            shownSecret = WebhookSignature.NewSecret();
            existing.RotateSecret(shownSecret);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new WebhookConfigResult(existing.Url, shownSecret, existing.IsActive));
    }
}
