using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Professionals.Commands;

public sealed record UpdateProfessionalCommand(
    Guid Id, string Name, string? Email, string? Phone,
    string? Specialty, string? Bio, string? AvatarUrl) : IRequest<Result>;

internal sealed class UpdateProfessionalCommandHandler(
    IProfessionalRepository professionalRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<UpdateProfessionalCommand, Result>
{
    public async Task<Result> Handle(
        UpdateProfessionalCommand request, CancellationToken cancellationToken)
    {
        var professional = await professionalRepository.GetByIdAsync(request.Id, cancellationToken);
        if (professional is null) return Result.Failure(ProfessionalErrors.NotFound);

        professional.Update(request.Name, request.Email, request.Phone,
            request.Specialty, request.Bio, request.AvatarUrl);

        professionalRepository.Update(professional);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
