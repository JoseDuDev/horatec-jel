using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Services.Commands;

public sealed record DeleteServiceCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteServiceCommandHandler(
    IServiceRepository serviceRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<DeleteServiceCommand, Result>
{
    public async Task<Result> Handle(
        DeleteServiceCommand request, CancellationToken cancellationToken)
    {
        var service = await serviceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (service is null) return Result.Failure(ServiceErrors.NotFound);

        service.Delete(currentUser.Email ?? "system");
        serviceRepository.Update(service);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
