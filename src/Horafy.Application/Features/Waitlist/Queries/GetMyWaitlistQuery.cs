using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Waitlist.Queries;

public sealed record WaitlistEntryResult(
    Guid Id,
    Guid ServiceId,
    Guid ResourceId,
    DateOnly PreferredDate,
    WaitlistStatus Status,
    DateTimeOffset CreatedAt);

public sealed record GetMyWaitlistQuery : IRequest<Result<IReadOnlyList<WaitlistEntryResult>>>;

internal sealed class GetMyWaitlistQueryHandler(
    IWaitlistRepository waitlistRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyWaitlistQuery, Result<IReadOnlyList<WaitlistEntryResult>>>
{
    public async Task<Result<IReadOnlyList<WaitlistEntryResult>>> Handle(
        GetMyWaitlistQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<IReadOnlyList<WaitlistEntryResult>>(Error.Unauthorized);

        var entries = await waitlistRepository.GetByCustomerAsync(
            currentUser.UserId.Value, cancellationToken);

        var result = entries.Select(e => new WaitlistEntryResult(
            e.Id, e.ServiceId, e.ResourceId, e.PreferredDate, e.Status, e.CreatedAt)).ToList();

        return Result.Success<IReadOnlyList<WaitlistEntryResult>>(result);
    }
}
