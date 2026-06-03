using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Tenants;

/// <summary>
/// Disparado quando um novo tenant é criado.
/// Consumers: provisionar schema no PostgreSQL, enviar e-mail de boas-vindas.
/// </summary>
public sealed record TenantCreatedEvent(
    Guid TenantId,
    string TenantName,
    string Slug) : DomainEvent;
