using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Leads;

/// <summary>
/// Lead de interesse capturado pela landing pública (sem cadastro self-service,
/// a aquisição é assistida: a equipe entra em contato via WhatsApp).
/// Tabela global (schema public) — existe antes de qualquer tenant.
/// </summary>
public sealed class PlatformLead : BaseEntity
{
    public string  Name         { get; private set; } = string.Empty;
    /// <summary>Telefone/WhatsApp informado pelo interessado.</summary>
    public string  Phone        { get; private set; } = string.Empty;
    public string? Email        { get; private set; }
    /// <summary>Tipo de negócio em texto livre (barbearia, clínica, quadra...).</summary>
    public string? BusinessType { get; private set; }
    public string? Message      { get; private set; }
    /// <summary>Marca de origem da landing (agenda, alugue, mjml).</summary>
    public string  Brand        { get; private set; } = string.Empty;
    /// <summary>Marcado pela equipe quando o contato foi feito.</summary>
    public bool    IsContacted  { get; private set; }

    private PlatformLead() { }

    public static PlatformLead Create(
        string name,
        string phone,
        string? email,
        string? businessType,
        string? message,
        string brand) => new()
    {
        Name         = name.Trim(),
        Phone        = phone.Trim(),
        Email        = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
        BusinessType = string.IsNullOrWhiteSpace(businessType) ? null : businessType.Trim(),
        Message      = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
        Brand        = brand.Trim().ToLowerInvariant()
    };

    public void MarkContacted() => IsContacted = true;
}
