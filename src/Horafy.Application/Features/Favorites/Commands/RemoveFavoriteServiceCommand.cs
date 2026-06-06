using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Favorites.Commands;

public sealed record RemoveFavoriteServiceCommand(Guid ServiceId) : IRequest<Result>;

internal sealed class RemoveFavoriteServiceCommandHandler(
    ICurrentUserService        currentUserService,
    IFavoriteServiceRepository repository,
    ITenantUnitOfWork          unitOfWork)
    : IRequestHandler<RemoveFavoriteServiceCommand, Result>
{
    public async Task<Result> Handle(
        RemoveFavoriteServiceCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
            return Result.Failure(new Error(
                "Customer.Unauthorized", "Usuário não autenticado.", ErrorType.Unauthorized));

        var favorite = await repository.GetAsync(
            currentUserService.UserId.Value, request.ServiceId, cancellationToken);

        if (favorite is null)
            return Result.Failure(new Error(
                "Favorite.NotFound",
                "Favorito não encontrado.",
                ErrorType.NotFound));

        repository.Remove(favorite);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
