using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Professionals.Queries;

public sealed record GetProfessionalByIdQuery(Guid Id) : IRequest<Result<ProfessionalResult>>;

internal sealed class GetProfessionalByIdQueryHandler(
    IProfessionalRepository professionalRepository)
    : IRequestHandler<GetProfessionalByIdQuery, Result<ProfessionalResult>>
{
    public async Task<Result<ProfessionalResult>> Handle(
        GetProfessionalByIdQuery request, CancellationToken cancellationToken)
    {
        var p = await professionalRepository.GetByIdAsync(request.Id, cancellationToken);
        if (p is null) return Result.Failure<ProfessionalResult>(ProfessionalErrors.NotFound);

        return Result.Success(new ProfessionalResult(p.Id, p.Name, p.Email, p.Phone,
            p.Specialty, p.Bio, p.AvatarUrl, p.IsActive));
    }
}
