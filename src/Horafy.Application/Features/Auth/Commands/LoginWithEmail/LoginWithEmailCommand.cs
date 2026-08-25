using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.LoginWithEmail;

/// <summary>
/// Login com e-mail OU celular + senha.
///
/// O campo <see cref="Email"/> aceita os dois formatos: contendo '@' é tratado
/// como e-mail (comportamento histórico); caso contrário é tratado como celular
/// (apenas dígitos), o que exige <see cref="TenantSlug"/> — o telefone só é
/// único dentro de um tenant.
/// </summary>
public sealed record LoginWithEmailCommand(
    string Email,
    string Password,
    string? TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class LoginWithEmailCommandHandler(
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<LoginWithEmailCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        LoginWithEmailCommand request,
        CancellationToken cancellationToken)
    {
        // Resolve o tenant do slug uma única vez (quando informado). Slug de tenant
        // inexistente devolve o MESMO erro de credenciais — anti-enumeração.
        Tenant? tenant = null;
        if (!string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            tenant = await tenantRepository.GetBySlugAsync(
                request.TenantSlug.Trim(), cancellationToken);
            if (tenant is null)
                return Result.Failure<TokenPair>(AuthErrors.InvalidCredentials);
        }

        var identifier = request.Email.Trim();
        var user = identifier.Contains('@')
            ? await userRepository.GetByEmailAsync(
                identifier.ToLowerInvariant(), cancellationToken)
            : await FindByPhoneAsync(identifier, tenant, cancellationToken);

        // Mesmo erro para usuário não encontrado e senha incorreta (evita user enumeration)
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return Result.Failure<TokenPair>(AuthErrors.InvalidCredentials);

        // Se o slug veio preenchido, o usuário precisa pertencer àquele tenant — sem
        // isso o token emitido seria rejeitado depois pelo TenantBindingMiddleware
        // (token "inútil" no portal). Usuários sem tenant (PlatformAdmin) passam:
        // operam entre tenants.
        if (tenant is not null && user.TenantId is not null && user.TenantId != tenant.Id)
            return Result.Failure<TokenPair>(AuthErrors.InvalidCredentials);

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result.Failure<TokenPair>(AuthErrors.InvalidCredentials);

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tokenService.GenerateTokens(user));
    }

    /// <summary>
    /// Busca por celular: exige tenant resolvido e tenta as variantes com/sem o
    /// prefixo 55. Zero ou mais de uma correspondência → null (vira InvalidCredentials).
    /// </summary>
    private async Task<User?> FindByPhoneAsync(
        string identifier, Tenant? tenant, CancellationToken cancellationToken)
    {
        if (tenant is null)
            return null;

        var digits = PhoneNumber.Normalize(identifier);
        if (!PhoneNumber.IsValid(digits))
            return null;

        var matches = await userRepository.GetByPhoneAsync(
            PhoneNumber.BuildCandidates(digits), tenant.Id, cancellationToken);

        return matches.Count == 1 ? matches[0] : null;
    }
}
