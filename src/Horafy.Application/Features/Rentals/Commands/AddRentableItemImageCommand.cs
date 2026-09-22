using Horafy.Application.Common.Images;
using Horafy.Application.Features.Rentals.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Commands;

/// <summary>Anexa uma foto à galeria do item. A primeira foto vira a capa.</summary>
public sealed record AddRentableItemImageCommand(Guid ItemId, ImageUpload Upload)
    : IRequest<Result<RentableItemImageResult>>;

internal sealed class AddRentableItemImageCommandHandler(
    IRentableItemRepository rentableItemRepository,
    IImageStorage imageStorage,
    ITenantUnitOfWork unitOfWork)
    : IRequestHandler<AddRentableItemImageCommand, Result<RentableItemImageResult>>
{
    public async Task<Result<RentableItemImageResult>> Handle(
        AddRentableItemImageCommand request, CancellationToken cancellationToken)
    {
        var item = await rentableItemRepository.GetByIdWithImagesAsync(request.ItemId, cancellationToken);
        if (item is null)
            return Result.Failure<RentableItemImageResult>(RentalErrors.ItemNotFound);

        // O teto é conferido antes de gravar: arquivo no disco de um upload que vai
        // ser recusado na linha seguinte é lixo que ninguém volta para limpar.
        if (item.Images.Count >= RentableItem.MaxImages)
            return Result.Failure<RentableItemImageResult>(
                ImageErrors.LimitReached(RentableItem.MaxImages));

        var validation = await ImageUploadPolicy.ValidateAsync(request.Upload, cancellationToken);
        if (validation.IsFailure)
            return Result.Failure<RentableItemImageResult>(validation.Error);

        var stored = await imageStorage.SaveAsync(
            request.Upload, validation.Value, "itens", cancellationToken);

        var image = item.AddImage(stored.Url);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RentableItemImageResult(image.Id, image.Url, image.SortOrder));
    }
}
