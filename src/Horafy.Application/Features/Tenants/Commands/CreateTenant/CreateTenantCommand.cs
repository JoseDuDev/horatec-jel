using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.CreateTenant;

/// <summary>
/// Onboarding completo de um novo tenant:
/// cria o Tenant, o usuário TenantOwner e provisiona o schema PostgreSQL.
/// </summary>
public sealed record CreateTenantCommand(
    // Dados do estabelecimento
    string Name,
    string Slug,
    TenantVertical Vertical,
    string? Email,
    string? Phone,
    string? City,
    string? State,
    // Dados do proprietário
    string OwnerName,
    string OwnerEmail,
    string OwnerPassword) : IRequest<Result<CreateTenantResult>>;

public sealed record CreateTenantResult(
    Guid TenantId,
    string Slug,
    TokenPair Tokens);

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug)
            .NotEmpty().MaximumLength(63)
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug deve conter apenas letras minúsculas, números e hífens.");
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.OwnerPassword)
            .NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Senha deve ter ao menos uma maiúscula.")
            .Matches(@"[0-9]").WithMessage("Senha deve ter ao menos um número.");
    }
}

internal sealed class CreateTenantCommandHandler(
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ITenantSchemaService schemaService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateTenantCommand, Result<CreateTenantResult>>
{
    public async Task<Result<CreateTenantResult>> Handle(
        CreateTenantCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Slug único
        if (await tenantRepository.SlugExistsAsync(request.Slug, cancellationToken))
            return Result.Failure<CreateTenantResult>(TenantErrors.SlugAlreadyTaken);

        // 2. E-mail do owner único
        if (await userRepository.ExistsByEmailAsync(request.OwnerEmail, cancellationToken))
            return Result.Failure<CreateTenantResult>(TenantErrors.OwnerEmailAlreadyRegistered);

        // 3. Cria o tenant
        var tenant = Tenant.Create(request.Name, request.Slug, request.Vertical, request.Email);
        tenantRepository.Add(tenant);

        // 4. Cria o usuário TenantOwner
        var owner = User.CreateWithEmail(
            request.OwnerEmail,
            passwordHasher.Hash(request.OwnerPassword),
            request.OwnerName,
            tenant.Id,
            UserRole.TenantOwner);

        userRepository.Add(owner);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Provisiona o schema PostgreSQL do tenant
        await schemaService.CreateSchemaAsync(request.Slug, cancellationToken);

        var tokens = tokenService.GenerateTokens(owner);

        return Result.Success(new CreateTenantResult(tenant.Id, tenant.Slug, tokens));
    }
}
