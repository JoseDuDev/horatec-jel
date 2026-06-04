using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Waitlist.Commands;

public sealed record LeaveWaitlistCommand(Guid EntryId) : IRequest<Result>;

internal sealed class LeaveWaitlistCommandHandler(
    IWaitlistRepository waitlistRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<LeaveWaitlistCommand, Result>
{
    public async Task<Result> Handle(LeaveWaitlistCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var entry = await waitlistRepository.GetByIdAsync(request.EntryId, cancellationToken);
        if (entry is null) return Result.Failure(WaitlistErrors.NotFound);

        if (entry.CustomerId != currentUser.UserId)
            return Result.Failure(Error.Unauthorized);

        entry.Cancel();
        waitlistRepository.Update(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
