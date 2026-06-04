using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

public sealed record SetAvailabilityExceptionCommand(
    Guid ResourceId,
    DateOnly Date,
    bool IsBlocked,
    TimeOnly? CustomStart,
    TimeOnly? CustomEnd,
    string? Reason) : IRequest<Result>;

public sealed class SetAvailabilityExceptionCommandValidator
    : AbstractValidator<SetAvailabilityExceptionCommand>
{
    public SetAvailabilityExceptionCommandValidator()
    {
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.CustomStart)
            .LessThan(x => x.CustomEnd)
            .When(x => !x.IsBlocked && x.CustomStart.HasValue && x.CustomEnd.HasValue)
            .WithMessage("Início deve ser anterior ao fim.");
        RuleFor(x => x.CustomStart)
            .NotNull()
            .When(x => !x.IsBlocked)
            .WithMessage("CustomStart é obrigatório quando não bloqueado.");
        RuleFor(x => x.CustomEnd)
            .NotNull()
            .When(x => !x.IsBlocked)
            .WithMessage("CustomEnd é obrigatório quando não bloqueado.");
    }
}

internal sealed class SetAvailabilityExceptionCommandHandler(
    IResourceRepository resourceRepository,
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<SetAvailabilityExceptionCommand, Result>
{
    public async Task<Result> Handle(
        SetAvailabilityExceptionCommand request, CancellationToken cancellationToken)
    {
        var resourceExists = await resourceRepository.ExistsAsync(
            r => r.Id == request.ResourceId, cancellationToken);
        if (!resourceExists)
            return Result.Failure(AvailabilityErrors.ResourceNotFound);

        var existing = await availabilityRepository
            .GetExceptionAsync(request.ResourceId, request.Date, cancellationToken);

        if (existing is not null)
            availabilityRepository.Remove(existing);

        var exception = request.IsBlocked
            ? AvailabilityException.CreateBlock(request.ResourceId, request.Date, request.Reason)
            : AvailabilityException.CreateCustomHours(
                request.ResourceId, request.Date,
                request.CustomStart!.Value, request.CustomEnd!.Value, request.Reason);

        availabilityRepository.Add(exception);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
