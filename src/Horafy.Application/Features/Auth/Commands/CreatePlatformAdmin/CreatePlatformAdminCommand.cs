using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.CreatePlatformAdmin;

/// <summary>
/// Cria um novo PlatformAdmin (superadmin) — evita que o admin seedado via
/// env var (<see cref="Infrastructure.Persistence.PlatformAdminSeeder"/>, chamado só
/// no bootstrap) vire ponto único de falha. Sem tenant/schema envolvidos.
/// </summary>
public sealed record CreatePlatformAdminCommand(
    string Email,
    string Password,
    string Name) : IRequest<Result<CreatePlatformAdminResult>>;

public sealed record CreatePlatformAdminResult(Guid Id, string Email, string Name);

public sealed class CreatePlatformAdminCommandValidator : AbstractValidator<CreatePlatformAdminCommand>
{
    public CreatePlatformAdminCommandValidator()
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
    }
}

internal sealed class CreatePlatformAdminCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : IRequestHandler<CreatePlatformAdminCommand, Result<CreatePlatformAdminResult>>
{
    public async Task<Result<CreatePlatformAdminResult>> Handle(
        CreatePlatformAdminCommand request,
        CancellationToken cancellationToken)
    {
        if (await userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            return Result.Failure<CreatePlatformAdminResult>(AuthErrors.EmailAlreadyRegistered);

        var admin = User.CreateWithEmail(
            request.Email,
            passwordHasher.Hash(request.Password),
            request.Name,
            tenantId: null,
            UserRole.PlatformAdmin);

        userRepository.Add(admin);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreatePlatformAdminResult(admin.Id, admin.Email, admin.Name!));
    }
}
