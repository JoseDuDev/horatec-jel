using Horafy.Shared;

namespace Horafy.Application.Features.Rentals;

public static class RentalErrors
{
    public static readonly Error ItemNotFound = new(
        "Rental.ItemNotFound", "Item de locação não encontrado.", ErrorType.NotFound);

    public static readonly Error ItemInactive = new(
        "Rental.ItemInactive", "Item de locação está inativo.", ErrorType.Validation);

    public static readonly Error InvalidPeriod = new(
        "Rental.InvalidPeriod",
        "A data de devolução deve ser posterior à data de retirada.",
        ErrorType.Validation);

    public static readonly Error PastDate = new(
        "Rental.PastDate",
        "Não é possível alugar para uma data no passado.",
        ErrorType.Validation);

    public static readonly Error OutOfStock = new(
        "Rental.OutOfStock",
        "Não há unidades disponíveis para o período selecionado.",
        ErrorType.Conflict);

    public static readonly Error BookingNotFound = new(
        "Rental.BookingNotFound", "Locação não encontrada.", ErrorType.NotFound);

    public static readonly Error NotARental = new(
        "Rental.NotARental", "A reserva informada não é uma locação.", ErrorType.Validation);

    public static Error InvalidLifecycleTransition(string detail) =>
        new("Rental.InvalidLifecycleTransition", detail, ErrorType.Validation);
}
