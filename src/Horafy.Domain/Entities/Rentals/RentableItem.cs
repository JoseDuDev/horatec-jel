using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Rentals;

/// <summary>
/// Item disponível para locação (ferramenta, brinquedo, item em geral).
/// Reside no schema tenant_{slug} — nunca no schema public.
/// A capacidade de locação simultânea é dada por <see cref="Quantity"/> (estoque).
/// </summary>
public sealed class RentableItem : BaseEntity
{
    private RentableItem() { } // EF Core

    public string  Name        { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Category    { get; private set; }

    /// <summary>Estoque total de unidades idênticas disponíveis para locação.</summary>
    public int Quantity { get; private set; }

    /// <summary>Valor da diária em moeda local (BRL).</summary>
    public decimal DailyRate { get; private set; }

    /// <summary>Caução exigida por unidade (BRL). 0 = sem caução.</summary>
    public decimal SecurityDeposit { get; private set; }

    /// <summary>Dias de bloqueio após a devolução (limpeza/conferência) antes de relocar.</summary>
    public int BufferDays { get; private set; }

    public string? ImageUrl { get; private set; }
    public bool    IsActive { get; private set; } = true;

    public static RentableItem Create(
        string name,
        int quantity,
        decimal dailyRate,
        decimal securityDeposit = 0,
        int bufferDays = 0,
        string? description = null,
        string? category = null,
        string? imageUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (quantity <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantity));
        if (dailyRate < 0)
            throw new ArgumentException("Diária não pode ser negativa.", nameof(dailyRate));
        if (securityDeposit < 0)
            throw new ArgumentException("Caução não pode ser negativa.", nameof(securityDeposit));
        if (bufferDays < 0)
            throw new ArgumentException("Buffer não pode ser negativo.", nameof(bufferDays));

        return new RentableItem
        {
            Name            = name.Trim(),
            Quantity        = quantity,
            DailyRate       = dailyRate,
            SecurityDeposit = securityDeposit,
            BufferDays      = bufferDays,
            Description     = description?.Trim(),
            Category        = category?.Trim(),
            ImageUrl        = imageUrl?.Trim(),
        };
    }

    public void Update(
        string name,
        int quantity,
        decimal dailyRate,
        decimal securityDeposit,
        int bufferDays,
        string? description,
        string? category,
        string? imageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (quantity <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantity));
        if (dailyRate < 0)
            throw new ArgumentException("Diária não pode ser negativa.", nameof(dailyRate));
        if (securityDeposit < 0)
            throw new ArgumentException("Caução não pode ser negativa.", nameof(securityDeposit));
        if (bufferDays < 0)
            throw new ArgumentException("Buffer não pode ser negativo.", nameof(bufferDays));

        Name            = name.Trim();
        Quantity        = quantity;
        DailyRate       = dailyRate;
        SecurityDeposit = securityDeposit;
        BufferDays      = bufferDays;
        Description     = description?.Trim();
        Category        = category?.Trim();
        ImageUrl        = imageUrl?.Trim();
        UpdatedAt       = DateTimeOffset.UtcNow;
    }

    public void Activate()   { IsActive = true;  UpdatedAt = DateTimeOffset.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
}
