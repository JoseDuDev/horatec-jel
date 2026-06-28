using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Availability;

/// <summary>
/// Data de bloqueio global do tenant: o estabelecimento fica fechado em todos os
/// recursos nessa data (ex.: feriado pontual, reforma). Diferente de <see cref="Holiday"/>,
/// é consultada diretamente no cálculo de disponibilidade — não precisa ser aplicada
/// recurso a recurso.
/// </summary>
public sealed class TenantBlackoutDate : BaseEntity
{
    private TenantBlackoutDate() { }

    public DateOnly Date   { get; private set; }
    public string?  Reason { get; private set; }

    public static TenantBlackoutDate Create(DateOnly date, string? reason = null) =>
        new()
        {
            Date   = date,
            Reason = reason?.Trim()
        };

    public void UpdateReason(string? reason)
    {
        Reason    = reason?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
