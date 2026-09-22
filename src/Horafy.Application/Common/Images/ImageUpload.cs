namespace Horafy.Application.Common.Images;

/// <summary>
/// Arquivo recebido para upload, desacoplado do ASP.NET (nada de IFormFile aqui:
/// a camada Application não conhece HTTP).
/// O <see cref="Content"/> é lido uma única vez pelo storage.
/// </summary>
public sealed record ImageUpload(
    Stream Content,
    string FileName,
    string ContentType,
    long   Length);
