using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record SetAvailabilityRuleCommand(
    Guid ResourceId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes,
    int BreakAfterMinutes = 0) : IRequest<Result>;

public sealed class SetAvailabilityRuleCommandValidator : AbstractValidator<SetAvailabilityRuleCommand>
{
    public SetAvailabilityRuleCommandValidator()
    {
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.StartTime).LessThan(x => x.EndTime)
            .WithMessage("Início deve ser anterior ao fim.");
        RuleFor(x => x.SlotDurationMinutes).GreaterThan(0);
        RuleFor(x => x.BreakAfterMinutes).GreaterThanOrEqualTo(0);
    }
}

internal sealed class SetAvailabilityRuleCommandHandler(
    IResourceRepository resourceRepository,
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<SetAvailabilityRuleCommand, Result>
{
    public async Task<Result> Handle(
        SetAvailabilityRuleCommand request, CancellationToken cancellationToken)
    {
        var resourceExists = await resourceRepository.ExistsAsync(
            r => r.Id == request.ResourceId, cancellationToken);
        if (!resourceExists)
            return Result.Failure(AvailabilityErrors.ResourceNotFound);

        var existing = await availabilityRepository
            .GetRuleAsync(request.ResourceId, request.DayOfWeek, cancellationToken);

        if (existing is null)
        {
            var rule = AvailabilityRule.Create(
                request.ResourceId, request.DayOfWeek,
                request.StartTime, request.EndTime,
                request.SlotDurationMinutes, request.BreakAfterMinutes);
            availabilityRepository.Add(rule);
        }
        else
        {
            existing.Update(request.StartTime, request.EndTime,
                request.SlotDurationMinutes, request.BreakAfterMinutes);
            availabilityRepository.Update(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
