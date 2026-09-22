using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Services.Commands;

/// <summary>Tira a foto do serviço e apaga o arquivo do storage.</summary>
public sealed record RemoveServiceImageCommand(Guid ServiceId) : IRequest<Result>;

internal sealed class RemoveServiceImageCommandHandler(
    IServiceRepository serviceRepository,
    IImageStorage imageStorage,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<RemoveServiceImageCommand, Result>
{
    public async Task<Result> Handle(
        RemoveServiceImageCommand request, CancellationToken cancellationToken)
    {
        var service = await serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken);
        if (service is null) return Result.Failure(ServiceErrors.NotFound);

        var url = service.ImageUrl;
        if (string.IsNullOrWhiteSpace(url)) return Result.Success();

        service.SetImage(null);
        serviceRepository.Update(service);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await imageStorage.DeleteAsync(url, cancellationToken);

        return Result.Success();
    }
}
