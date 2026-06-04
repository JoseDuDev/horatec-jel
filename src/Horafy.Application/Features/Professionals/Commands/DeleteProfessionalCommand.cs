using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Professionals.Commands;

public sealed record DeleteProfessionalCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteProfessionalCommandHandler(
    IProfessionalRepository professionalRepository,
    ICurrentUserService currentUser,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<DeleteProfessionalCommand, Result>
{
    public async Task<Result> Handle(
        DeleteProfessionalCommand request, CancellationToken cancellationToken)
    {
        var professional = await professionalRepository.GetByIdAsync(request.Id, cancellationToken);
        if (professional is null) return Result.Failure(ProfessionalErrors.NotFound);

        professional.Delete(currentUser.Email ?? "system");
        professionalRepository.Update(professional);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
