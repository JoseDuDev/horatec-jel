using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Professionals.Queries;

public sealed record GetProfessionalsQuery(bool OnlyActive = true)
    : IRequest<Result<IReadOnlyList<ProfessionalResult>>>;

public sealed record ProfessionalResult(
    Guid Id, string Name, string? Email, string? Phone,
    string? Specialty, string? Bio, string? AvatarUrl, bool IsActive);

internal sealed class GetProfessionalsQueryHandler(
    IProfessionalRepository professionalRepository)
    : IRequestHandler<GetProfessionalsQuery, Result<IReadOnlyList<ProfessionalResult>>>
{
    public async Task<Result<IReadOnlyList<ProfessionalResult>>> Handle(
        GetProfessionalsQuery request, CancellationToken cancellationToken)
    {
        var professionals = request.OnlyActive
            ? await professionalRepository.GetActiveAsync(cancellationToken)
            : await professionalRepository.GetAllAsync(cancellationToken);

        var result = professionals
            .Select(p => new ProfessionalResult(p.Id, p.Name, p.Email, p.Phone,
                p.Specialty, p.Bio, p.AvatarUrl, p.IsActive))
            .ToList();

        return Result.Success<IReadOnlyList<ProfessionalResult>>(result);
    }
}
