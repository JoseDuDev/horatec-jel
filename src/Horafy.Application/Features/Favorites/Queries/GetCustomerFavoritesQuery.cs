using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Favorites.Queries;

public sealed record GetCustomerFavoritesQuery
    : IRequest<Result<IReadOnlyList<FavoriteServiceResult>>>;

public sealed record FavoriteServiceResult(Guid Id, Guid ServiceId, DateTimeOffset CreatedAt);

internal sealed class GetCustomerFavoritesQueryHandler(
    ICurrentUserService        currentUserService,
    IFavoriteServiceRepository repository)
    : IRequestHandler<GetCustomerFavoritesQuery, Result<IReadOnlyList<FavoriteServiceResult>>>
{
    public async Task<Result<IReadOnlyList<FavoriteServiceResult>>> Handle(
        GetCustomerFavoritesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
            return Result.Failure<IReadOnlyList<FavoriteServiceResult>>(new Error(
                "Customer.Unauthorized", "Usuário não autenticado.", ErrorType.Unauthorized));

        var favorites = await repository.GetByCustomerAsync(
            currentUserService.UserId.Value, cancellationToken);

        var results = favorites
            .Select(f => new FavoriteServiceResult(f.Id, f.ServiceId, f.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<FavoriteServiceResult>>(results);
    }
}
