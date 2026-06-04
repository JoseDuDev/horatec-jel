using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.LoginWithEmail;

public sealed record LoginWithEmailCommand(
    string Email,
    string Password,
    string? TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class LoginWithEmailCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<LoginWithEmailCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        LoginWithEmailCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(
            request.Email.ToLowerInvariant().Trim(), cancellationToken);

        // Mesmo erro para e-mail não encontrado e senha incorreta (evita user enumeration)
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return Result.Failure<TokenPair>(AuthErrors.InvalidCredentials);

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result.Failure<TokenPair>(AuthErrors.InvalidCredentials);

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
