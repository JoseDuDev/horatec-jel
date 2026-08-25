using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.RegisterWithEmail;

public sealed record RegisterWithEmailCommand(
    string Email,
    string Password,
    string Name,
    string? TenantSlug,
    string? Phone = null,
    UserRole Role = UserRole.Customer) : IRequest<Result<TokenPair>>;

public sealed class RegisterWithEmailCommandValidator : AbstractValidator<RegisterWithEmailCommand>
{
    public RegisterWithEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-mail é obrigatório.")
            .EmailAddress().WithMessage("E-mail inválido.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.")
            .Matches(@"[A-Z]").WithMessage("Senha deve conter ao menos uma letra maiúscula.")
            .Matches(@"[0-9]").WithMessage("Senha deve conter ao menos um número.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(150);

        // Celular opcional: validado sobre a forma normalizada (dígitos-somente).
        RuleFor(x => x.Phone)
            .Must(p => PhoneNumber.IsValid(PhoneNumber.Normalize(p)))
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Celular inválido. Informe DDD + número (10 a 13 dígitos).");
    }
}

internal sealed class RegisterWithEmailCommandHandler(
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<RegisterWithEmailCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        RegisterWithEmailCommand request,
        CancellationToken cancellationToken)
    {
        // Verifica duplicidade de e-mail
        if (await userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            return Result.Failure<TokenPair>(AuthErrors.EmailAlreadyRegistered);

        // Resolve tenant opcional
        Guid? tenantId = null;
        if (!string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken);
            if (tenant is null)
                return Result.Failure<TokenPair>(AuthErrors.TenantNotFound);
            tenantId = tenant.Id;
        }

        // Celular opcional, guardado normalizado (dígitos-somente). Deve ser único
        // dentro do tenant — considerando as variantes com/sem o prefixo 55, para
        // não criar ambiguidade no login por celular.
        var phone = PhoneNumber.Normalize(request.Phone);
        if (phone.Length > 0 && tenantId.HasValue)
        {
            var samePhone = await userRepository.GetByPhoneAsync(
                PhoneNumber.BuildCandidates(phone), tenantId.Value, cancellationToken);
            if (samePhone.Count > 0)
                return Result.Failure<TokenPair>(AuthErrors.PhoneAlreadyRegistered);
        }

        var passwordHash = passwordHasher.Hash(request.Password);

        var user = User.CreateWithEmail(
            request.Email, passwordHash, request.Name, tenantId, request.Role);

        if (phone.Length > 0)
            user.SetPhone(phone);

        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
