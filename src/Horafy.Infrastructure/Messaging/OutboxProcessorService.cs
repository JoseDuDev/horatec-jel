using System.Text.Json;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Persistence.Interceptors;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.Messaging;

internal sealed class OutboxProcessorService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessorService> logger) : BackgroundService
{
    private const int MaxRetries      = 3;
    private const int BatchSize       = 20;
    private const int IntervalSeconds = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Erro no OutboxProcessorService");
            }

            await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
        }
    }

    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<HorafyDbContext>();
        var bus     = scope.ServiceProvider.GetRequiredService<IBus>();

        var messages = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.Type);
                if (type is null)
                {
                    message.Error      = $"Tipo não encontrado: {message.Type}";
                    message.RetryCount = MaxRetries;
                    continue;
                }

                var payload = JsonSerializer.Deserialize(message.Content, type);
                if (payload is not null)
                    await bus.Publish(payload, type, ct);

                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.Error       = null;

                logger.LogInformation("Outbox message {Id} publicado: {Type}", message.Id, type.Name);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                logger.LogWarning(ex,
                    "Falha ao processar outbox message {Id} (tentativa {Retry}/{Max})",
                    message.Id, message.RetryCount, MaxRetries);
            }
        }

        if (messages.Count > 0)
            await context.SaveChangesAsync(ct);
    }
}
