using Horafy.Application.Common.Images;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Services.Commands;

/// <summary>Grava a foto do serviço e substitui a anterior, se houver.</summary>
public sealed record SetServiceImageCommand(Guid ServiceId, ImageUpload Upload)
    : IRequest<Result<string>>;

internal sealed class SetServiceImageCommandHandler(
    IServiceRepository serviceRepository,
    IImageStorage imageStorage,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<SetServiceImageCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        SetServiceImageCommand request, CancellationToken cancellationToken)
    {
        var service = await serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken);
        if (service is null) return Result.Failure<string>(ServiceErrors.NotFound);

        var validation = await ImageUploadPolicy.ValidateAsync(request.Upload, cancellationToken);
        if (validation.IsFailure)
            return Result.Failure<string>(validation.Error);

        var previousUrl = service.ImageUrl;

        var stored = await imageStorage.SaveAsync(
            request.Upload, validation.Value, "servicos", cancellationToken);

        service.SetImage(stored.Url);
        serviceRepository.Update(service);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Serviço só tem uma foto: a antiga vira lixo no disco assim que a nova entra.
        if (!string.IsNullOrWhiteSpace(previousUrl))
            await imageStorage.DeleteAsync(previousUrl, cancellationToken);

        return Result.Success(stored.Url);
    }
}
