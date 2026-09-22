using System.Text.RegularExpressions;
using Horafy.Application.Common.Images;
using Horafy.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Horafy.Infrastructure.Storage;

/// <summary>
/// Guarda as imagens no disco do servidor, sob um volume, e as serve pela rota
/// pública <c>/uploads</c> (configurada no Program.cs).
///
/// Layout: <c>{raiz}/{slug-do-tenant}/{pasta}/{aaaaMM}/{guid}{ext}</c>.
/// O slug sai do tenant resolvido na requisição, nunca de parâmetro — é o que
/// impede um tenant de gravar (ou apagar) na pasta de outro.
/// </summary>
internal sealed partial class LocalDiskImageStorage(
    ICurrentTenantService currentTenant,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ImageStorageOptions> options,
    ILogger<LocalDiskImageStorage> logger) : IImageStorage
{
    private readonly ImageStorageOptions _options = options.Value;

    public async Task<StoredImage> SaveAsync(
        ImageUpload upload,
        ImageFormat format,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var tenantSegment = TenantSegment();
        var folderSegment = SafeSegment(folder, nameof(folder));
        var monthSegment  = DateTimeOffset.UtcNow.ToString("yyyyMM");
        var fileName      = $"{Guid.NewGuid():N}{format.Extension}";

        var relativePath  = string.Join('/', tenantSegment, folderSegment, monthSegment, fileName);
        var absolutePath  = Path.Combine(_options.RootPath,
            tenantSegment, folderSegment, monthSegment, fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var file = new FileStream(
            absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81_920, useAsync: true))
        {
            await upload.Content.CopyToAsync(file, cancellationToken);
        }

        logger.LogInformation("Imagem gravada: {Path}", relativePath);

        return new StoredImage(PublicUrl(relativePath), relativePath);
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        var relativePath = RelativePathFrom(url);

        // Fora do padrão (ex.: URL externa colada à mão antes de existir upload):
        // não é nosso arquivo, não há o que apagar.
        if (relativePath is null) return Task.CompletedTask;

        var absolutePath = Path.GetFullPath(Path.Combine(_options.RootPath, relativePath));
        var root         = Path.GetFullPath(_options.RootPath);

        // Cinto e suspensório contra "../": o caminho final tem que continuar dentro da raiz
        // e dentro da pasta do tenant que está na requisição.
        if (!absolutePath.StartsWith(root, StringComparison.Ordinal) ||
            !relativePath.StartsWith($"{TenantSegment()}/", StringComparison.Ordinal))
        {
            logger.LogWarning("Descartado delete fora do escopo do tenant: {Url}", url);
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(absolutePath)) File.Delete(absolutePath);
        }
        catch (IOException ex)
        {
            // Arquivo órfão custa bytes; falhar o request por causa disso custa o cadastro do cliente.
            logger.LogWarning(ex, "Não foi possível apagar a imagem {Path}", relativePath);
        }

        return Task.CompletedTask;
    }

    // ── Internos ──────────────────────────────────────────────────────────────

    private string TenantSegment()
    {
        var slug = currentTenant.Slug;

        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidOperationException(
                "Upload de imagem exige um tenant resolvido na requisição.");

        return SafeSegment(slug, nameof(slug));
    }

    private static string SafeSegment(string value, string paramName)
    {
        var trimmed = value.Trim().ToLowerInvariant();

        if (!SegmentPattern().IsMatch(trimmed))
            throw new ArgumentException($"Segmento de caminho inválido: '{value}'.", paramName);

        return trimmed;
    }

    private string PublicUrl(string relativePath)
    {
        var requestPath = _options.RequestPath.TrimEnd('/');
        var baseUrl     = (_options.PublicBaseUrl ?? string.Empty).TrimEnd('/');

        if (string.IsNullOrEmpty(baseUrl))
        {
            var request = httpContextAccessor.HttpContext?.Request;
            if (request is not null)
                baseUrl = $"{request.Scheme}://{request.Host}";
        }

        return $"{baseUrl}{requestPath}/{relativePath}";
    }

    private string? RelativePathFrom(string url)
    {
        var marker = $"{_options.RequestPath.TrimEnd('/')}/";
        var index  = url.IndexOf(marker, StringComparison.Ordinal);

        if (index < 0) return null;

        var relativePath = url[(index + marker.Length)..];

        return relativePath.Contains("..", StringComparison.Ordinal) ? null : relativePath;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$")]
    private static partial Regex SegmentPattern();
}
