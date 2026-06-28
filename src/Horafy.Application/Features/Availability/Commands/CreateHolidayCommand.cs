using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record CreateHolidayCommand(
    string  Name,
    DateOnly Date,
    bool    IsRecurringAnnually,
    string? Reason) : IRequest<Result<Guid>>;

public sealed class CreateHolidayCommandValidator : AbstractValidator<CreateHolidayCommand>
{
    public CreateHolidayCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CreateHolidayCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork       unitOfWork)
    : IRequestHandler<CreateHolidayCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateHolidayCommand request, CancellationToken cancellationToken)
    {
        var holiday = Holiday.Create(
            request.Name,
            request.Date,
            request.IsRecurringAnnually,
            request.Reason);

        availabilityRepository.Add(holiday);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(holiday.Id);
    }
}
