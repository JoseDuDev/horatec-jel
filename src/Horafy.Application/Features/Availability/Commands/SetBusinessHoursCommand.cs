using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record SetBusinessHoursCommand(
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    bool IsOpen) : IRequest<Result>;

public sealed class SetBusinessHoursCommandValidator : AbstractValidator<SetBusinessHoursCommand>
{
    public SetBusinessHoursCommandValidator()
    {
        RuleFor(x => x.OpenTime)
            .LessThan(x => x.CloseTime)
            .When(x => x.IsOpen)
            .WithMessage("Horário de abertura deve ser anterior ao de fechamento.");
    }
}

internal sealed class SetBusinessHoursCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<SetBusinessHoursCommand, Result>
{
    public async Task<Result> Handle(
        SetBusinessHoursCommand request, CancellationToken cancellationToken)
    {
        var existing = await availabilityRepository
            .GetBusinessHoursByDayAsync(request.DayOfWeek, cancellationToken);

        if (existing is null)
        {
            var bh = BusinessHours.Create(
                request.DayOfWeek, request.OpenTime, request.CloseTime, request.IsOpen);
            availabilityRepository.Add(bh);
        }
        else
        {
            existing.Update(request.OpenTime, request.CloseTime, request.IsOpen);
            availabilityRepository.Update(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
