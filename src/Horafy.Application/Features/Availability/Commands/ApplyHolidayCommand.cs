using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record ApplyHolidayCommand(
    Guid                  HolidayId,
    IReadOnlyList<Guid>   ResourceIds) : IRequest<Result>;

public sealed class ApplyHolidayCommandValidator : AbstractValidator<ApplyHolidayCommand>
{
    public ApplyHolidayCommandValidator()
    {
        RuleFor(x => x.HolidayId).NotEmpty();
        RuleFor(x => x.ResourceIds).NotEmpty()
            .WithMessage("Ao menos um recurso deve ser informado.");
    }
}

internal sealed class ApplyHolidayCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork       unitOfWork)
    : IRequestHandler<ApplyHolidayCommand, Result>
{
    public async Task<Result> Handle(
        ApplyHolidayCommand request, CancellationToken cancellationToken)
    {
        var holiday = await availabilityRepository
            .GetHolidayAsync(request.HolidayId, cancellationToken);

        if (holiday is null)
            return Result.Failure(AvailabilityErrors.HolidayNotFound);

        foreach (var resourceId in request.ResourceIds)
        {
            var existing = await availabilityRepository
                .GetExceptionAsync(resourceId, holiday.Date, cancellationToken);

            if (existing is not null)
                availabilityRepository.Remove(existing);

            availabilityRepository.Add(
                AvailabilityException.CreateBlock(resourceId, holiday.Date, holiday.Name));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
