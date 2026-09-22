using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Rentals;

/// <summary>
/// Foto de um <see cref="RentableItem"/>. Reside no schema tenant_{slug}.
/// A ordem de exibição é dada por <see cref="SortOrder"/>; a de menor valor é a capa.
/// Só é criada/removida através do agregado (<c>RentableItem.AddImage</c> etc.),
/// que mantém a sequência sem buracos e o <c>ImageUrl</c> do item em sincronia.
/// </summary>
public sealed class RentableItemImage : BaseEntity
{
    private RentableItemImage() { } // EF Core

    public Guid   RentableItemId { get; private set; }
    public string Url            { get; private set; } = default!;

    /// <summary>Posição na galeria, começando em 0. A posição 0 é a capa.</summary>
    public int SortOrder { get; private set; }

    internal static RentableItemImage Create(Guid rentableItemId, string url, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (sortOrder < 0)
            throw new ArgumentException("Ordem não pode ser negativa.", nameof(sortOrder));

        return new RentableItemImage
        {
            RentableItemId = rentableItemId,
            Url            = url.Trim(),
            SortOrder      = sortOrder,
        };
    }

    internal void MoveTo(int sortOrder)
    {
        if (sortOrder < 0)
            throw new ArgumentException("Ordem não pode ser negativa.", nameof(sortOrder));

        if (SortOrder == sortOrder) return;

        SortOrder = sortOrder;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
