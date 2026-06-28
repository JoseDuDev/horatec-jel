using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record UpdateHolidayCommand(
    Guid    Id,
    string  Name,
    DateOnly Date,
    bool    IsRecurringAnnually,
    string? Reason) : IRequest<Result>;

public sealed class UpdateHolidayCommandValidator : AbstractValidator<UpdateHolidayCommand>
{
    public UpdateHolidayCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateHolidayCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork       unitOfWork)
    : IRequestHandler<UpdateHolidayCommand, Result>
{
    public async Task<Result> Handle(
        UpdateHolidayCommand request, CancellationToken cancellationToken)
    {
        var holiday = await availabilityRepository
            .GetHolidayAsync(request.Id, cancellationToken);

        if (holiday is null)
            return Result.Failure(AvailabilityErrors.HolidayNotFound);

        holiday.Update(request.Name, request.Date, request.IsRecurringAnnually, request.Reason);
        availabilityRepository.Update(holiday);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
