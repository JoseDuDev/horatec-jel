using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Waitlist.Commands;

public sealed record JoinWaitlistCommand(
    Guid ServiceId,
    Guid ResourceId,
    DateOnly PreferredDate) : IRequest<Result<Guid>>;

internal sealed class JoinWaitlistCommandHandler(
    IWaitlistRepository waitlistRepository,
    IServiceRepository serviceRepository,
    IResourceRepository resourceRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<JoinWaitlistCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(JoinWaitlistCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized);

        var service = await serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken);
        if (service is null) return Result.Failure<Guid>(WaitlistErrors.ServiceNotFound);

        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null) return Result.Failure<Guid>(WaitlistErrors.ResourceNotFound);

        var alreadyWaiting = await waitlistRepository.ExistsActiveAsync(
            request.ServiceId, request.ResourceId,
            currentUser.UserId.Value, request.PreferredDate, cancellationToken);

        if (alreadyWaiting) return Result.Failure<Guid>(WaitlistErrors.AlreadyInQueue);

        var entry = WaitlistEntry.Create(
            request.ServiceId, request.ResourceId,
            currentUser.UserId.Value,
            currentUser.Email ?? "Cliente",
            currentUser.Email ?? string.Empty,
            request.PreferredDate);

        waitlistRepository.Add(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(entry.Id);
    }
}
