namespace Horafy.Shared;

/// <summary>
/// Representa um erro de domínio ou aplicação de forma estruturada.
/// </summary>
public sealed record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static readonly Error NullValue = new(
        "General.NullValue",
        "Um valor nulo foi fornecido onde não era esperado.",
        ErrorType.Failure);

    public static readonly Error NotFound = new(
        "General.NotFound",
        "O recurso solicitado não foi encontrado.",
        ErrorType.NotFound);

    public static readonly Error Unauthorized = new(
        "General.Unauthorized",
        "Você não tem permissão para realizar esta operação.",
        ErrorType.Unauthorized);

    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    public static Error NotFoundFor(string resource, object id) =>
        new($"{resource}.NotFound", $"{resource} com id '{id}' não foi encontrado.", ErrorType.NotFound);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);
}

public enum ErrorType
{
    None,
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized
}
