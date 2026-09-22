using Horafy.Shared;

namespace Horafy.Application.Common.Images;

/// <summary>Formato aceito, já normalizado — nunca vem do que o cliente declarou.</summary>
public sealed record ImageFormat(string Extension, string ContentType);

/// <summary>
/// Porteiro único de todo upload de imagem: tamanho e formato.
///
/// O formato é decidido pelos bytes iniciais do arquivo, não pelo Content-Type nem
/// pela extensão — os dois vêm do cliente e mentem de graça. Sem isso, um .svg ou
/// um .html renomeado para .jpg seria servido do nosso domínio e viraria XSS,
/// já que a pasta de uploads é pública.
/// </summary>
public static class ImageUploadPolicy
{
    /// <summary>Teto por arquivo. O navegador já reduz antes de enviar; isto é a rede de segurança
    /// para quem chama a API direto.</summary>
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly ImageFormat Jpeg = new(".jpg",  "image/jpeg");
    private static readonly ImageFormat Png  = new(".png",  "image/png");
    private static readonly ImageFormat Webp = new(".webp", "image/webp");

    public static IReadOnlyList<string> AcceptedContentTypes { get; } =
        [Jpeg.ContentType, Png.ContentType, Webp.ContentType];

    public static async Task<Result<ImageFormat>> ValidateAsync(
        ImageUpload upload, CancellationToken cancellationToken = default)
    {
        if (upload.Length <= 0)
            return Result.Failure<ImageFormat>(ImageErrors.Empty);

        if (upload.Length > MaxBytes)
            return Result.Failure<ImageFormat>(ImageErrors.TooLarge(MaxBytes));

        var header = new byte[12];
        var read   = await ReadHeaderAsync(upload.Content, header, cancellationToken);

        var format = Detect(header.AsSpan(0, read));

        return format is null
            ? Result.Failure<ImageFormat>(ImageErrors.UnsupportedFormat)
            : Result.Success(format);
    }

    private static async Task<int> ReadHeaderAsync(
        Stream content, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var chunk = await content.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (chunk == 0) break;
            read += chunk;
        }

        // Rebobina para o storage gravar o arquivo inteiro, inclusive o cabeçalho lido aqui.
        if (content.CanSeek) content.Seek(0, SeekOrigin.Begin);

        return read;
    }

    private static ImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        // JPEG: FF D8 FF
        if (header.Length >= 3 &&
            header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return Jpeg;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return Png;

        // WebP: "RIFF" nos bytes 0-3 e "WEBP" nos bytes 8-11
        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            return Webp;

        return null;
    }
}
