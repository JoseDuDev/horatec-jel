using Horafy.Application.Common.Images;

namespace Horafy.API.Extensions;

public static class FormFileExtensions
{
    /// <summary>
    /// Converte o arquivo do multipart no contrato que a Application entende.
    /// O stream sai posicionado no início e é lido uma vez só, pelo storage.
    /// </summary>
    public static ImageUpload ToImageUpload(this IFormFile file) =>
        new(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
}
