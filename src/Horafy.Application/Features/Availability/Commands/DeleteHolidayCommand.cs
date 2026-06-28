using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record DeleteHolidayCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteHolidayCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork       unitOfWork)
    : IRequestHandler<DeleteHolidayCommand, Result>
{
    public async Task<Result> Handle(
        DeleteHolidayCommand request, CancellationToken cancellationToken)
    {
        var holiday = await availabilityRepository
            .GetHolidayAsync(request.Id, cancellationToken);

        if (holiday is null)
            return Result.Failure(AvailabilityErrors.HolidayNotFound);

        availabilityRepository.Remove(holiday);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
