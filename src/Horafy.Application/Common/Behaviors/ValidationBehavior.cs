using FluentValidation;
using MediatR;
using Horafy.Shared;

namespace Horafy.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior do MediatR que executa validações FluentValidation
/// antes de qualquer Command ou Query chegar ao handler.
/// Retorna Result de falha com erros de validação em vez de lançar exceção.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var errors = failures
            .Select(f => Error.Validation(f.PropertyName, f.ErrorMessage))
            .ToArray();

        // Retorna o primeiro erro de validação como Result de falha
        return CreateValidationResult<TResponse>(errors[0]);
    }

    private static TResult CreateValidationResult<TResult>(Error error)
        where TResult : Result
    {
        if (typeof(TResult) == typeof(Result))
            return (TResult)Result.Failure(error);

        // Result<T> via reflection (mantém type-safety)
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var failureMethod = typeof(Result)
            .GetMethod(nameof(Result.Failure))!
            .MakeGenericMethod(resultType);

        return (TResult)failureMethod.Invoke(null, [error])!;
    }
}
