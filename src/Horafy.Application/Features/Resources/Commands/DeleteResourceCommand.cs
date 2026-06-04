using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record DeleteResourceCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteResourceCommandHandler(
    IResourceRepository resourceRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<DeleteResourceCommand, Result>
{
    public async Task<Result> Handle(
        DeleteResourceCommand request, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (resource is null) return Result.Failure(ResourceErrors.NotFound);

        resource.Delete(currentUser.Email ?? "system");
        resourceRepository.Update(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
