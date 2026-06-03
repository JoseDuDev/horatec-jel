using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace Horafy.API.Middleware;

/// <summary>
/// Captura exceções não tratadas e retorna ProblemDetails padronizado (RFC 7807).
/// Mapeia exceções conhecidas para status HTTP apropriados.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Exceção não tratada: {Message} | Path: {Path} | TraceId: {TraceId}",
                ex.Message,
                context.Request.Path,
                context.TraceIdentifier);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "Argumento inválido"),
            ArgumentException => (HttpStatusCode.BadRequest, "Requisição inválida"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Recurso não encontrado"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Não autorizado"),
            InvalidOperationException => (HttpStatusCode.UnprocessableEntity, "Operação inválida"),
            TimeoutException => (HttpStatusCode.GatewayTimeout, "Tempo limite excedido"),
            _ => (HttpStatusCode.InternalServerError, "Erro interno do servidor")
        };

        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingMiddleware(
        this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
