using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Customers.Commands;

public sealed record UpdateCustomerPhoneCommand(string? Phone) : IRequest<Result>;

internal sealed class UpdateCustomerPhoneCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository     userRepository,
    IUnitOfWork         unitOfWork)
    : IRequestHandler<UpdateCustomerPhoneCommand, Result>
{
    public async Task<Result> Handle(
        UpdateCustomerPhoneCommand request, CancellationToken cancellationToken)
    {
        if (request.Phone is not null && request.Phone.Length > 20)
            return Result.Failure(new Error(
                "Customer.PhoneTooLong",
                "O telefone deve ter no máximo 20 caracteres.",
                ErrorType.Validation));

        if (!currentUserService.UserId.HasValue)
            return Result.Failure(new Error(
                "Customer.Unauthorized", "Usuário não autenticado.", ErrorType.Unauthorized));

        var user = await userRepository.GetByIdAsync(
            currentUserService.UserId.Value, cancellationToken);

        if (user is null)
            return Result.Failure(new Error(
                "Customer.NotFound", "Usuário não encontrado.", ErrorType.NotFound));

        user.SetPhone(request.Phone);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
