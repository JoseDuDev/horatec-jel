using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record DeleteAvailabilityExceptionCommand(
    Guid ResourceId,
    DateOnly Date) : IRequest<Result>;

internal sealed class DeleteAvailabilityExceptionCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<DeleteAvailabilityExceptionCommand, Result>
{
    public async Task<Result> Handle(
        DeleteAvailabilityExceptionCommand request, CancellationToken cancellationToken)
    {
        var existing = await availabilityRepository
            .GetExceptionAsync(request.ResourceId, request.Date, cancellationToken);

        if (existing is null)
            return Result.Failure(AvailabilityErrors.ExceptionNotFound);

        availabilityRepository.Remove(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
