using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Horafy.Application.Common.Behaviors;

/// <summary>
/// Loga início, conclusão e tempo de execução de todos os Commands e Queries.
/// Alerta para operações lentas (> 500ms) via log de Warning.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        logger.LogInformation(
            "[Horafy] Iniciando {RequestName}",
            requestName);

        try
        {
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                logger.LogWarning(
                    "[Horafy] Requisição lenta detectada: {RequestName} levou {ElapsedMs}ms",
                    requestName,
                    sw.ElapsedMilliseconds);
            }
            else
            {
                logger.LogInformation(
                    "[Horafy] {RequestName} concluído em {ElapsedMs}ms",
                    requestName,
                    sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(
                ex,
                "[Horafy] Erro ao processar {RequestName} após {ElapsedMs}ms",
                requestName,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
