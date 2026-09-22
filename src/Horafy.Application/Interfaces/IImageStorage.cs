using Horafy.Application.Common.Images;

namespace Horafy.Application.Interfaces;

/// <summary>Imagem já gravada: a URL pública e a chave usada para apagá-la depois.</summary>
public sealed record StoredImage(string Url, string Key);

/// <summary>
/// Onde as imagens enviadas pelos clientes ficam guardadas.
///
/// Existe para que a troca de disco local por um bucket (R2/S3) seja configuração,
/// não reescrita: a Application só conhece esta interface. A implementação é quem
/// separa os arquivos por tenant — o chamador não escolhe pasta de outro tenant.
/// </summary>
public interface IImageStorage
{
    /// <param name="folder">Agrupador lógico dentro do tenant (ex.: "itens", "servicos").</param>
    Task<StoredImage> SaveAsync(
        ImageUpload upload,
        ImageFormat format,
        string folder,
        CancellationToken cancellationToken = default);

    /// <summary>Apaga pela URL devolvida no upload. Ignora silenciosamente o que já não existe.</summary>
    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
}
