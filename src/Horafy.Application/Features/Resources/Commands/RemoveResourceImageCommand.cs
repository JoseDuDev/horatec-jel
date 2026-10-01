using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

/// <summary>Tira a foto do recurso e apaga o arquivo do storage.</summary>
public sealed record RemoveResourceImageCommand(Guid ResourceId) : IRequest<Result>;

internal sealed class RemoveResourceImageCommandHandler(
    IResourceRepository resourceRepository,
    IImageStorage imageStorage,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<RemoveResourceImageCommand, Result>
{
    public async Task<Result> Handle(
        RemoveResourceImageCommand request, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure(ResourceErrors.NotFound);

        var url = resource.AvatarUrl;
        if (string.IsNullOrWhiteSpace(url)) return Result.Success();

        resource.SetAvatar(null);
        resourceRepository.Update(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await imageStorage.DeleteAsync(url, cancellationToken);

        return Result.Success();
    }
}
