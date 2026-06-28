using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Commands;

/// <summary>Cria um bloqueio global (fecha o estabelecimento em todos os recursos na data).</summary>
public sealed record CreateBlackoutDateCommand(
    DateOnly Date,
    string?  Reason) : IRequest<Result<Guid>>;

public sealed class CreateBlackoutDateCommandValidator : AbstractValidator<CreateBlackoutDateCommand>
{
    public CreateBlackoutDateCommandValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

internal sealed class CreateBlackoutDateCommandHandler(
    IAvailabilityRepository availabilityRepository,
    ITenantUnitOfWork       unitOfWork)
    : IRequestHandler<CreateBlackoutDateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateBlackoutDateCommand request, CancellationToken cancellationToken)
    {
        var existing = await availabilityRepository.GetBlackoutDateAsync(request.Date, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(AvailabilityErrors.BlackoutAlreadyExists);

        var blackout = TenantBlackoutDate.Create(request.Date, request.Reason);
        availabilityRepository.Add(blackout);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(blackout.Id);
    }
}
