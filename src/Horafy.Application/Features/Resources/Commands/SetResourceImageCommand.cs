using Horafy.Application.Common.Images;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

/// <summary>Grava a foto do recurso e substitui a anterior, se houver.</summary>
public sealed record SetResourceImageCommand(Guid ResourceId, ImageUpload Upload)
    : IRequest<Result<string>>;

internal sealed class SetResourceImageCommandHandler(
    IResourceRepository resourceRepository,
    IImageStorage imageStorage,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<SetResourceImageCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        SetResourceImageCommand request, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure<string>(ResourceErrors.NotFound);

        var validation = await ImageUploadPolicy.ValidateAsync(request.Upload, cancellationToken);
        if (validation.IsFailure)
            return Result.Failure<string>(validation.Error);

        var previousUrl = resource.AvatarUrl;

        var stored = await imageStorage.SaveAsync(
            request.Upload, validation.Value, "recursos", cancellationToken);

        resource.SetAvatar(stored.Url);
        resourceRepository.Update(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Recurso só tem uma foto: a antiga vira lixo no disco assim que a nova entra.
        if (!string.IsNullOrWhiteSpace(previousUrl))
            await imageStorage.DeleteAsync(previousUrl, cancellationToken);

        return Result.Success(stored.Url);
    }
}
