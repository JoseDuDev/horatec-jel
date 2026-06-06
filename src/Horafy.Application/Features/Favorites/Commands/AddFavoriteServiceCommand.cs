using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Favorites.Commands;

public sealed record AddFavoriteServiceCommand(Guid ServiceId) : IRequest<Result>;

internal sealed class AddFavoriteServiceCommandHandler(
    ICurrentUserService        currentUserService,
    IFavoriteServiceRepository repository,
    ITenantUnitOfWork          unitOfWork)
    : IRequestHandler<AddFavoriteServiceCommand, Result>
{
    public async Task<Result> Handle(
        AddFavoriteServiceCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
            return Result.Failure(new Error(
                "Customer.Unauthorized", "Usuário não autenticado.", ErrorType.Unauthorized));

        var existing = await repository.GetAsync(
            currentUserService.UserId.Value, request.ServiceId, cancellationToken);

        if (existing is not null)
            return Result.Failure(new Error(
                "Favorite.AlreadyExists",
                "Este serviço já está nos seus favoritos.",
                ErrorType.Conflict));

        var favorite = FavoriteService.Create(currentUserService.UserId.Value, request.ServiceId);
        repository.Add(favorite);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
