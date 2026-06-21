using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Queries;

public sealed record GetRentableItemsQuery(bool OnlyActive = true)
    : IRequest<Result<IReadOnlyList<RentableItemResult>>>;

public sealed record RentableItemResult(
    Guid    Id,
    string  Name,
    string? Description,
    string? Category,
    int     Quantity,
    decimal DailyRate,
    decimal SecurityDeposit,
    int     BufferDays,
    string? ImageUrl,
    bool    IsActive);

internal sealed class GetRentableItemsQueryHandler(
    IRentableItemRepository rentableItemRepository)
    : IRequestHandler<GetRentableItemsQuery, Result<IReadOnlyList<RentableItemResult>>>
{
    public async Task<Result<IReadOnlyList<RentableItemResult>>> Handle(
        GetRentableItemsQuery request, CancellationToken cancellationToken)
    {
        var items = request.OnlyActive
            ? await rentableItemRepository.GetActiveAsync(cancellationToken)
            : await rentableItemRepository.GetAllAsync(cancellationToken);

        var result = items
            .Select(i => new RentableItemResult(
                i.Id, i.Name, i.Description, i.Category, i.Quantity,
                i.DailyRate, i.SecurityDeposit, i.BufferDays, i.ImageUrl, i.IsActive))
            .ToList();

        return Result.Success<IReadOnlyList<RentableItemResult>>(result);
    }
}
