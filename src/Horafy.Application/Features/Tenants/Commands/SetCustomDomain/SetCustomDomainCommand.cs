using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.SetCustomDomain;

/// <summary>
/// Vincula (ou atualiza) um domínio próprio ao tenant atual.
/// O cliente deve configurar um CNAME apontando para a plataforma.
/// </summary>
public sealed record SetCustomDomainCommand(string Domain) : IRequest<Result>;

public sealed class SetCustomDomainCommandValidator : AbstractValidator<SetCustomDomainCommand>
{
    public SetCustomDomainCommandValidator()
    {
        RuleFor(x => x.Domain)
            .NotEmpty()
            .MaximumLength(253)
            .Matches(@"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$")
            .WithMessage("Formato de domínio inválido. Ex: minhaclinica.com.br");
    }
}

internal sealed class SetCustomDomainCommandHandler(
    ICurrentTenantService currentTenant,
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<SetCustomDomainCommand, Result>
{
    public async Task<Result> Handle(
        SetCustomDomainCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var domain = request.Domain.ToLowerInvariant().Trim();

        // Verifica se outro tenant já usa este domínio
        var isTaken = await tenantRepository.IsDomainTakenAsync(
            domain, excludeTenantId: currentTenant.TenantId.Value, cancellationToken);

        if (isTaken) return Result.Failure(TenantErrors.DomainAlreadyTaken);

        var tenant = await tenantRepository.GetByIdAsync(
            currentTenant.TenantId.Value, cancellationToken);

        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.SetCustomDomain(domain);
        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
