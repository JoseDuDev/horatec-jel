using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Rentals.Commands;

/// <summary>
/// Altera os dados de um item já cadastrado.
///
/// Não recebe foto: a capa é decidida pela galeria (ver <c>RentableItem.AddImage</c>
/// e os endpoints de imagem). Passar uma URL aqui é como o campo nasceu, antes da
/// galeria existir, e não é mais o caminho.
/// </summary>
public sealed record UpdateRentableItemCommand(
    Guid    Id,
    string  Name,
    int     Quantity,
    decimal DailyRate,
    decimal SecurityDeposit,
    int     BufferDays,
    string? Description,
    string? Category,
    bool    IsActive) : IRequest<Result>;

public sealed class UpdateRentableItemCommandValidator : AbstractValidator<UpdateRentableItemCommand>
{
    public UpdateRentableItemCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.DailyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SecurityDeposit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BufferDays).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateRentableItemCommandHandler(
    IRentableItemRepository rentableItemRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<UpdateRentableItemCommand, Result>
{
    public async Task<Result> Handle(
        UpdateRentableItemCommand request, CancellationToken cancellationToken)
    {
        // Com a galeria carregada, e não pelo GetByIdAsync: o agregado decide a capa
        // olhando as fotos que tem. Carregado sem elas, ele se acharia sem foto
        // nenhuma e apagaria a capa do item a cada edição de preço.
        var item = await rentableItemRepository.GetByIdWithImagesAsync(request.Id, cancellationToken);
        if (item is null) return Result.Failure(RentalErrors.ItemNotFound);

        item.Update(
            request.Name, request.Quantity, request.DailyRate,
            request.SecurityDeposit, request.BufferDays,
            request.Description, request.Category,
            imageUrl: null);

        if (request.IsActive) item.Activate();
        else item.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
