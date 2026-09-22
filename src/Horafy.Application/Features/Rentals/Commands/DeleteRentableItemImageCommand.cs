using Horafy.Application.Common.Images;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Commands;

/// <summary>Remove uma foto da galeria e o arquivo correspondente do storage.</summary>
public sealed record DeleteRentableItemImageCommand(Guid ItemId, Guid ImageId) : IRequest<Result>;

internal sealed class DeleteRentableItemImageCommandHandler(
    IRentableItemRepository rentableItemRepository,
    IImageStorage imageStorage,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<DeleteRentableItemImageCommand, Result>
{
    public async Task<Result> Handle(
        DeleteRentableItemImageCommand request, CancellationToken cancellationToken)
    {
        var item = await rentableItemRepository.GetByIdWithImagesAsync(request.ItemId, cancellationToken);
        if (item is null) return Result.Failure(RentalErrors.ItemNotFound);

        var image = item.Images.FirstOrDefault(i => i.Id == request.ImageId);
        if (image is null) return Result.Failure(ImageErrors.NotFound);

        var url = image.Url;

        if (!item.RemoveImage(request.ImageId))
            return Result.Failure(ImageErrors.NotFound);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Só depois que o banco confirmou: se a ordem fosse inversa, uma falha no
        // SaveChanges deixaria o item apontando para um arquivo que já não existe.
        await imageStorage.DeleteAsync(url, cancellationToken);

        return Result.Success();
    }
}
