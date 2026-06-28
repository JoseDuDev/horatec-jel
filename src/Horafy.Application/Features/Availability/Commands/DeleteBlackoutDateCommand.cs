using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record DeleteBlackoutDateCommand(DateOnly Date) : IRequest<Result>;

internal sealed class DeleteBlackoutDateCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork       unitOfWork)
    : IRequestHandler<DeleteBlackoutDateCommand, Result>
{
    public async Task<Result> Handle(
        DeleteBlackoutDateCommand request, CancellationToken cancellationToken)
    {
        var blackout = await availabilityRepository.GetBlackoutDateAsync(request.Date, cancellationToken);
        if (blackout is null)
            return Result.Failure(AvailabilityErrors.BlackoutNotFound);

        availabilityRepository.Remove(blackout);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
