namespace Horafy.Domain.Entities.Rentals;

/// <summary>
/// Orçamento de uma locação: valor das diárias, caução (reembolsável) e total a cobrar.
/// </summary>
/// <param name="RentalAmount">Soma das diárias (diária × dias × unidades).</param>
/// <param name="DepositAmount">Caução retida (reembolsável na devolução).</param>
/// <param name="Total">Valor total a cobrar = <see cref="RentalAmount"/> + <see cref="DepositAmount"/>.</param>
public readonly record struct RentalQuote(decimal RentalAmount, decimal DepositAmount, decimal Total);

/// <summary>
/// Cálculo de preço de locação. Modelo linear por diária — a tabela escalonada
/// (diária/semanal) pode ser adicionada aqui sem afetar os chamadores.
/// </summary>
public static class RentalPricing
{
    /// <summary>Número de diárias entre retirada e devolução (devolução exclusiva).</summary>
    public static int DaysBetween(DateOnly start, DateOnly end)
    {
        if (end <= start)
            throw new ArgumentException("A devolução deve ser posterior à retirada.", nameof(end));
        return end.DayNumber - start.DayNumber;
    }

    /// <summary>
    /// Calcula o orçamento de uma linha de locação (um item × unidades × dias).
    /// </summary>
    public static RentalQuote Calculate(decimal dailyRate, int days, int quantity, decimal securityDeposit)
    {
        if (dailyRate < 0)
            throw new ArgumentException("Diária não pode ser negativa.", nameof(dailyRate));
        if (days <= 0)
            throw new ArgumentException("A locação deve ter pelo menos 1 diária.", nameof(days));
        if (quantity < 1)
            throw new ArgumentException("Quantidade deve ser pelo menos 1.", nameof(quantity));
        if (securityDeposit < 0)
            throw new ArgumentException("Caução não pode ser negativa.", nameof(securityDeposit));

        var rental  = decimal.Round(dailyRate * days * quantity, 2);
        var deposit = decimal.Round(securityDeposit * quantity, 2);

        return new RentalQuote(rental, deposit, rental + deposit);
    }

    /// <summary>Sobrecarga por período (DateOnly), derivando o número de diárias.</summary>
    public static RentalQuote Calculate(
        decimal dailyRate, DateOnly start, DateOnly end, int quantity, decimal securityDeposit) =>
        Calculate(dailyRate, DaysBetween(start, end), quantity, securityDeposit);
}
