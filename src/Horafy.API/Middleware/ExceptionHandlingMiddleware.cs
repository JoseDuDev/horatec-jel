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
        // UnauthorizedAccessException NÃO vira 401: no .NET ela é erro de permissão de
        // disco ("Access to the path ... is denied"), não de login — a autenticação
        // responde 401 antes de chegar aqui. Mapeada para 401, o frontend tomava por
        // sessão expirada, reenviava o upload e, se o refresh falhasse, deslogava o
        // usuário no meio do cadastro (26/09/2026).
        var (statusCode, title) = exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "Argumento inválido"),
            ArgumentException => (HttpStatusCode.BadRequest, "Requisição inválida"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Recurso não encontrado"),
            InvalidOperationException => (HttpStatusCode.UnprocessableEntity, "Operação inválida"),
            TimeoutException => (HttpStatusCode.GatewayTimeout, "Tempo limite excedido"),
            _ => (HttpStatusCode.InternalServerError, "Erro interno do servidor")
        };

        // No 500 a mensagem da exceção fica só no log: ela carrega caminhos e
        // detalhes do servidor, e o frontend mostra o `detail` ao cliente. O traceId
        // basta para achar a linha no log. Nos mapeados acima a mensagem segue —
        // as do domínio são frases escritas para o cliente.
        var detail = statusCode == HttpStatusCode.InternalServerError
            ? $"Ocorreu um erro inesperado. Se persistir, informe o código {context.TraceIdentifier} ao suporte."
            : exception.Message;

        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
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
