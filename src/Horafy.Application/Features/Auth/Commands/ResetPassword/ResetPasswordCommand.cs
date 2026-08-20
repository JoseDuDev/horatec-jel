using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<Result>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token é obrigatório.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.")
            .Matches(@"[A-Z]").WithMessage("Senha deve conter ao menos uma letra maiúscula.")
            .Matches(@"[0-9]").WithMessage("Senha deve conter ao menos um número.");
    }
}

internal sealed class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = PasswordResetTokenHasher.Hash(request.Token);

        var user = await userRepository.GetByPasswordResetTokenHashAsync(tokenHash, cancellationToken);

        if (user is null ||
            user.PasswordResetExpiresAt is null ||
            user.PasswordResetExpiresAt < DateTimeOffset.UtcNow)
        {
            return Result.Failure(AuthErrors.InvalidOrExpiredResetToken);
        }

        user.SetPasswordHash(passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordResetToken();
        userRepository.Update(user);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
