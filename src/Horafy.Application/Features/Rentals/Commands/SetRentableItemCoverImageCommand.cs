using Horafy.Application.Common.Images;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Commands;

/// <summary>Promove uma foto já enviada a capa do item (a que aparece na vitrine).</summary>
public sealed record SetRentableItemCoverImageCommand(Guid ItemId, Guid ImageId) : IRequest<Result>;

internal sealed class SetRentableItemCoverImageCommandHandler(
    IRentableItemRepository rentableItemRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<SetRentableItemCoverImageCommand, Result>
{
    public async Task<Result> Handle(
        SetRentableItemCoverImageCommand request, CancellationToken cancellationToken)
    {
        var item = await rentableItemRepository.GetByIdWithImagesAsync(request.ItemId, cancellationToken);
        if (item is null) return Result.Failure(RentalErrors.ItemNotFound);

        if (!item.SetCoverImage(request.ImageId))
            return Result.Failure(ImageErrors.NotFound);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
