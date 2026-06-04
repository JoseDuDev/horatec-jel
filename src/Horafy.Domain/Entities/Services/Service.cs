using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Services;

/// <summary>
/// Serviço oferecido pelo tenant (ex: corte de cabelo, consulta, aluguel de quadra).
/// Reside no schema tenant_{slug} — nunca no schema public.
/// </summary>
public sealed class Service : BaseEntity
{
    private Service() { } // EF Core

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    /// <summary>Duração padrão do serviço em minutos.</summary>
    public int DurationMinutes { get; private set; }

    /// <summary>Preço em moeda local (BRL).</summary>
    public decimal Price { get; private set; }

    public string? Category { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Service Create(
        string name,
        int durationMinutes,
        decimal price,
        string? description = null,
        string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (durationMinutes <= 0)
            throw new ArgumentException("Duração deve ser maior que zero.", nameof(durationMinutes));

        if (price < 0)
            throw new ArgumentException("Preço não pode ser negativo.", nameof(price));

        return new Service
        {
            Name            = name.Trim(),
            DurationMinutes = durationMinutes,
            Price           = price,
            Description     = description?.Trim(),
            Category        = category?.Trim()
        };
    }

    public void Update(string name, int durationMinutes, decimal price,
        string? description, string? category)
    {
        Name            = name.Trim();
        DurationMinutes = durationMinutes;
        Price           = price;
        Description     = description?.Trim();
        Category        = category?.Trim();
        UpdatedAt       = DateTimeOffset.UtcNow;
    }

    public void Activate()   { IsActive = true;  UpdatedAt = DateTimeOffset.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
}
