using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Solicita a redefinição de senha. Sempre retorna sucesso, exista ou não o
/// e-mail — mesmo princípio anti-enumeração já usado em LoginWithEmailCommand.
/// </summary>
public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-mail é obrigatório.")
            .EmailAddress().WithMessage("E-mail inválido.");
    }
}

internal sealed class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IEmailService emailService,
    IPlatformUrlService platformUrl,
    IUnitOfWork unitOfWork) : IRequestHandler<ForgotPasswordCommand, Result>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(
            request.Email.ToLowerInvariant().Trim(), cancellationToken);

        // Mesmo se o usuário não existir (ou não tiver senha local), retorna sucesso
        // genérico — não revela se o e-mail está cadastrado.
        if (user is null)
            return Result.Success();

        var rawToken  = Guid.NewGuid().ToString("N");
        var tokenHash = PasswordResetTokenHasher.Hash(rawToken);
        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);

        user.SetPasswordResetToken(tokenHash, expiresAt);
        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // O host sai da requisição, não da configuração: o mesmo backend serve
        // AGENDA e ALUGUE, e um host fixo mandaria o admin de uma locadora para a
        // marca errada.
        var link = platformUrl.BuildUrl($"/reset-password?token={rawToken}");

        await emailService.SendAsync(
            user.Email,
            "Redefinição de senha",
            $"""
            <p>Recebemos uma solicitação para redefinir a senha da sua conta.</p>
            <p><a href="{link}">Clique aqui para criar uma nova senha</a></p>
            <p>Este link expira em 1 hora. Se você não solicitou isso, ignore este e-mail.</p>
            """,
            cancellationToken);

        return Result.Success();
    }
}
