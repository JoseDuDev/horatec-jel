using System.Text.Json;
using Horafy.Domain.Entities.Base;
using Horafy.Domain.Events.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Horafy.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Outbox Pattern: antes de salvar, converte Domain Events em registros
/// na tabela outbox_messages (mesma transação).
///
/// Um background job (OutboxProcessor) lê essa tabela periodicamente,
/// publica no RabbitMQ e marca como processado.
/// Isso garante at-least-once delivery mesmo em caso de falha da mensageria.
/// </summary>
public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            AddOutboxMessages(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            AddOutboxMessages(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private static void AddOutboxMessages(DbContext context)
    {
        var domainEvents = context.ChangeTracker
            .Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        if (domainEvents.Count == 0) return;

        var outboxMessages = domainEvents.Select(domainEvent => new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredAt = domainEvent.OccurredAt,
            Type = domainEvent.GetType().AssemblyQualifiedName!,
            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(),
                new JsonSerializerOptions { WriteIndented = false })
        });

        context.Set<OutboxMessage>().AddRange(outboxMessages);
    }
}

/// <summary>
/// Entidade da tabela outbox_messages para persistência dos Domain Events.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string Type { get; init; } = default!;
    public string Content { get; init; } = default!;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}
