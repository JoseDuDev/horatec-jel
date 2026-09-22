namespace Horafy.Infrastructure.Storage;

/// <summary>
/// Configuração do armazenamento de imagens (seção <c>Storage:Images</c>).
///
/// Hoje os arquivos ficam em disco, num volume do container. Quando a plataforma
/// passar para um bucket (R2/S3), o que muda é a implementação de IImageStorage —
/// estes dois valores continuam descrevendo "onde grava" e "por qual URL se lê".
/// </summary>
public sealed class ImageStorageOptions
{
    public const string SectionName = "Storage:Images";

    /// <summary>Raiz no disco. Precisa ser um volume, senão o deploy seguinte apaga as fotos.</summary>
    public string RootPath { get; set; } = "uploads";

    /// <summary>
    /// Base pública das URLs geradas (ex.: <c>https://api.mjml.com.br</c>).
    /// Vazio = monta a partir do host da requisição, o que resolve o dev em localhost.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Prefixo da rota que serve os arquivos.</summary>
    public string RequestPath { get; set; } = "/uploads";
}
