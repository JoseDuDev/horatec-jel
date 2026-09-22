using Horafy.Shared;

namespace Horafy.Application.Common.Images;

public static class ImageErrors
{
    public static readonly Error Empty = new(
        "Image.Empty", "Nenhum arquivo foi enviado.", ErrorType.Validation);

    public static Error TooLarge(long maxBytes) => new(
        "Image.TooLarge",
        $"A imagem excede o tamanho máximo de {maxBytes / (1024 * 1024)} MB.",
        ErrorType.Validation);

    public static readonly Error UnsupportedFormat = new(
        "Image.UnsupportedFormat",
        "Formato não suportado. Envie uma imagem JPG, PNG ou WebP.",
        ErrorType.Validation);

    public static readonly Error NotFound = new(
        "Image.NotFound", "Imagem não encontrada.", ErrorType.NotFound);

    public static Error LimitReached(int max) => new(
        "Image.LimitReached",
        $"Limite de {max} fotos por item atingido. Remova uma antes de enviar outra.",
        ErrorType.Validation);

    public static readonly Error StorageFailure = new(
        "Image.StorageFailure",
        "Não foi possível salvar a imagem. Tente novamente.",
        ErrorType.Failure);
}
