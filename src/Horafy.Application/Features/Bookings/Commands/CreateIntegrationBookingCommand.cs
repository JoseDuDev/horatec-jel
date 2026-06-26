using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

/// <summary>
/// Cria um agendamento em nome de um cliente a partir de uma integração (ex.: Atendefy),
/// com idempotência por <see cref="ExternalId"/> e marcação de <see cref="Source"/>.
/// </summary>
public sealed record CreateIntegrationBookingCommand(
    IReadOnlyList<Guid> ServiceIds,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? Notes,
    string? ExternalId,
    string? Source) : IRequest<Result<IntegrationBookingResult>>;

/// <summary><see cref="AlreadyExisted"/> = true quando o ExternalId já havia criado a reserva.</summary>
public sealed record IntegrationBookingResult(Guid BookingId, bool AlreadyExisted);

public sealed class CreateIntegrationBookingCommandValidator
    : AbstractValidator<CreateIntegrationBookingCommand>
{
    public CreateIntegrationBookingCommandValidator()
    {
        RuleFor(x => x.ServiceIds).NotEmpty().WithMessage("Pelo menos um serviço é obrigatório.");
        RuleForEach(x => x.ServiceIds).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.ScheduledAt)
            .Must(d => d > DateTimeOffset.UtcNow)
            .WithMessage("O horário deve ser futuro.");
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExternalId).MaximumLength(128);
        RuleFor(x => x.Source).MaximumLength(40);
    }
}

internal sealed class CreateIntegrationBookingCommandHandler(
    IServiceRepository  serviceRepository,
    IResourceRepository resourceRepository,
    IBookingRepository  bookingRepository,
    ITenantUnitOfWork   unitOfWork)
    : IRequestHandler<CreateIntegrationBookingCommand, Result<IntegrationBookingResult>>
{
    public async Task<Result<IntegrationBookingResult>> Handle(
        CreateIntegrationBookingCommand request, CancellationToken cancellationToken)
    {
        // 1. Idempotência: se o ExternalId já gerou uma reserva, devolve a mesma.
        var externalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim();
        if (externalId is not null)
        {
            var existing = await bookingRepository.GetByExternalIdAsync(externalId, cancellationToken);
            if (existing is not null)
                return Result.Success(new IntegrationBookingResult(existing.Id, AlreadyExisted: true));
        }

        // 2. Recurso e serviços
        var resource = await resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null)
            return Result.Failure<IntegrationBookingResult>(BookingErrors.ResourceNotFound);

        var fetchedServices = await serviceRepository.GetByIdsAsync(request.ServiceIds, cancellationToken);
        var serviceMap = fetchedServices.ToDictionary(s => s.Id);

        var services = new List<(Guid ServiceId, string ServiceName, int DurationMinutes, decimal Price)>();
        foreach (var serviceId in request.ServiceIds)
        {
            if (!serviceMap.TryGetValue(serviceId, out var service))
                return Result.Failure<IntegrationBookingResult>(BookingErrors.ServiceNotFound);
            services.Add((service.Id, service.Name, service.DurationMinutes, service.Price));
        }

        // 3. Conflito de horário
        var totalDuration = services.Sum(s => s.DurationMinutes);
        var endsAt = request.ScheduledAt.AddMinutes(totalDuration);

        var hasConflict = await bookingRepository.HasConflictAsync(
            request.ResourceId, request.ScheduledAt, endsAt, cancellationToken: cancellationToken);
        if (hasConflict)
            return Result.Failure<IntegrationBookingResult>(BookingErrors.Conflict);

        // 4. Cria com origem/idempotência
        var booking = Booking.Create(
            services,
            request.ResourceId,
            resource.Name,
            customerId:    Guid.NewGuid(),
            customerName:  request.CustomerName,
            customerEmail: request.CustomerEmail ?? string.Empty,
            scheduledAt:   request.ScheduledAt,
            customerPhone: request.CustomerPhone,
            notes:         request.Notes);

        booking.SetIntegrationOrigin(
            string.IsNullOrWhiteSpace(request.Source) ? BookingSource.Atendefy : request.Source!,
            externalId);

        bookingRepository.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new IntegrationBookingResult(booking.Id, AlreadyExisted: false));
    }
}
