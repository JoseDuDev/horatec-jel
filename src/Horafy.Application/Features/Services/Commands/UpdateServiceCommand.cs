using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Services.Commands;

public sealed record UpdateServiceCommand(
    Guid Id,
    string Name,
    int DurationMinutes,
    decimal Price,
    string? Description,
    string? Category) : IRequest<Result>;

internal sealed class UpdateServiceCommandHandler(
    IServiceRepository serviceRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<UpdateServiceCommand, Result>
{
    public async Task<Result> Handle(
        UpdateServiceCommand request, CancellationToken cancellationToken)
    {
        var service = await serviceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (service is null) return Result.Failure(ServiceErrors.NotFound);

        service.Update(request.Name, request.DurationMinutes, request.Price,
            request.Description, request.Category);

        serviceRepository.Update(service);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
