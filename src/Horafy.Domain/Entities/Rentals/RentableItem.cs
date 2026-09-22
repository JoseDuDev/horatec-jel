using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Rentals;

/// <summary>
/// Item disponível para locação (ferramenta, brinquedo, item em geral).
/// Reside no schema tenant_{slug} — nunca no schema public.
/// A capacidade de locação simultânea é dada por <see cref="Quantity"/> (estoque).
/// </summary>
public sealed class RentableItem : BaseEntity
{
    /// <summary>Teto de fotos por item. Locação vende no olho, mas galeria infinita
    /// vira peso de página e custo de disco — seis cobre o item por todos os lados.</summary>
    public const int MaxImages = 6;

    private readonly List<RentableItemImage> _images = [];

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

    /// <summary>
    /// Capa do item — espelho da primeira foto da galeria, mantido em coluna própria
    /// porque o catálogo, o portal e as integrações já leem esse campo.
    /// Quem escreve é o agregado; não altere por fora.
    /// </summary>
    public string? ImageUrl { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Galeria do item, já na ordem de exibição (a primeira é a capa).</summary>
    public IReadOnlyList<RentableItemImage> Images =>
        _images.OrderBy(i => i.SortOrder).ToList();

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
        Validate(quantity, dailyRate, securityDeposit, bufferDays);

        var item = new RentableItem
        {
            Name            = name.Trim(),
            Quantity        = quantity,
            DailyRate       = dailyRate,
            SecurityDeposit = securityDeposit,
            BufferDays      = bufferDays,
            Description     = description?.Trim(),
            Category        = category?.Trim(),
        };

        // URL informada na criação entra como a primeira foto da galeria: assim existe
        // um caminho de leitura só (Images) para quem monta a vitrine.
        if (!string.IsNullOrWhiteSpace(imageUrl))
            item.AddImage(imageUrl);

        return item;
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
        Validate(quantity, dailyRate, securityDeposit, bufferDays);

        Name            = name.Trim();
        Quantity        = quantity;
        DailyRate       = dailyRate;
        SecurityDeposit = securityDeposit;
        BufferDays      = bufferDays;
        Description     = description?.Trim();
        Category        = category?.Trim();

        // Item com galeria tem a capa decidida pela galeria — um imageUrl solto aqui
        // não pode sobrescrever a foto que o lojista escolheu como capa.
        if (_images.Count == 0)
            ImageUrl = imageUrl?.Trim();

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // ── Galeria ───────────────────────────────────────────────────────────────

    /// <summary>Acrescenta uma foto ao fim da galeria. A primeira vira a capa.</summary>
    /// <exception cref="InvalidOperationException">Quando já há <see cref="MaxImages"/> fotos.</exception>
    public RentableItemImage AddImage(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (_images.Count >= MaxImages)
            throw new InvalidOperationException(
                $"Item já tem o máximo de {MaxImages} fotos.");

        var image = RentableItemImage.Create(Id, url, _images.Count);
        _images.Add(image);
        Touch();

        return image;
    }

    /// <summary>Remove a foto e reordena o restante. Remover a capa promove a seguinte.</summary>
    /// <returns><c>false</c> quando a foto não pertence a este item.</returns>
    public bool RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image is null) return false;

        _images.Remove(image);
        Resequence();
        Touch();

        return true;
    }

    /// <summary>Promove a foto a capa, empurrando as demais uma posição para trás.</summary>
    /// <returns><c>false</c> quando a foto não pertence a este item.</returns>
    public bool SetCoverImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image is null) return false;

        var reordered = new List<RentableItemImage> { image };
        reordered.AddRange(_images.OrderBy(i => i.SortOrder).Where(i => i.Id != imageId));

        for (var position = 0; position < reordered.Count; position++)
            reordered[position].MoveTo(position);

        Touch();
        return true;
    }

    public void Activate()   { IsActive = true;  UpdatedAt = DateTimeOffset.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }

    // ── Internos ──────────────────────────────────────────────────────────────

    private static void Validate(int quantity, decimal dailyRate, decimal securityDeposit, int bufferDays)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantity));
        if (dailyRate < 0)
            throw new ArgumentException("Diária não pode ser negativa.", nameof(dailyRate));
        if (securityDeposit < 0)
            throw new ArgumentException("Caução não pode ser negativa.", nameof(securityDeposit));
        if (bufferDays < 0)
            throw new ArgumentException("Buffer não pode ser negativo.", nameof(bufferDays));
    }

    /// <summary>Fecha os buracos de ordem deixados por uma remoção.</summary>
    private void Resequence()
    {
        var ordered = _images.OrderBy(i => i.SortOrder).ToList();
        for (var position = 0; position < ordered.Count; position++)
            ordered[position].MoveTo(position);
    }

    /// <summary>Reespelha a capa e carimba a alteração.</summary>
    private void Touch()
    {
        ImageUrl  = _images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
