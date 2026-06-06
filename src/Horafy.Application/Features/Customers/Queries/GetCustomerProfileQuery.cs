using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Customers.Queries;

public sealed record GetCustomerProfileQuery : IRequest<Result<CustomerProfileResult>>;

public sealed record CustomerProfileResult(
    Guid    Id,
    string  Name,
    string  Email,
    string? Phone,
    string? AvatarUrl);

internal sealed class GetCustomerProfileQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository     userRepository)
    : IRequestHandler<GetCustomerProfileQuery, Result<CustomerProfileResult>>
{
    public async Task<Result<CustomerProfileResult>> Handle(
        GetCustomerProfileQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
            return Result.Failure<CustomerProfileResult>(new Error(
                "Customer.Unauthorized", "Usuário não autenticado.", ErrorType.Unauthorized));

        var user = await userRepository.GetByIdAsync(
            currentUserService.UserId.Value, cancellationToken);

        if (user is null)
            return Result.Failure<CustomerProfileResult>(new Error(
                "Customer.NotFound", "Usuário não encontrado.", ErrorType.NotFound));

        return Result.Success(new CustomerProfileResult(
            user.Id, user.Name ?? user.Email, user.Email, user.Phone, user.AvatarUrl));
    }
}
