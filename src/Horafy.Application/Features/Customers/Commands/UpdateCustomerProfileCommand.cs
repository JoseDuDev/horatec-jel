using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Customers.Commands;

public sealed record UpdateCustomerProfileCommand(
    string? Name,
    string? AvatarUrl) : IRequest<Result>;

public sealed class UpdateCustomerProfileCommandValidator : AbstractValidator<UpdateCustomerProfileCommand>
{
    public UpdateCustomerProfileCommandValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.AvatarUrl).MaximumLength(500).When(x => x.AvatarUrl is not null);
    }
}

internal sealed class UpdateCustomerProfileCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository     userRepository,
    IUnitOfWork         unitOfWork)
    : IRequestHandler<UpdateCustomerProfileCommand, Result>
{
    public async Task<Result> Handle(
        UpdateCustomerProfileCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
            return Result.Failure(new Error(
                "Customer.Unauthorized", "Usuário não autenticado.", ErrorType.Unauthorized));

        var user = await userRepository.GetByIdAsync(
            currentUserService.UserId.Value, cancellationToken);

        if (user is null)
            return Result.Failure(new Error(
                "Customer.NotFound", "Usuário não encontrado.", ErrorType.NotFound));

        user.UpdateProfile(request.Name, request.AvatarUrl);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
