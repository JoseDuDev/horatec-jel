using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Professionals;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Professionals.Commands;

public sealed record CreateProfessionalCommand(
    string Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    string? AvatarUrl,
    Guid? UserId) : IRequest<Result<Guid>>;

public sealed class CreateProfessionalCommandValidator : AbstractValidator<CreateProfessionalCommand>
{
    public CreateProfessionalCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
    }
}

internal sealed class CreateProfessionalCommandHandler(
    IProfessionalRepository professionalRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateProfessionalCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateProfessionalCommand request, CancellationToken cancellationToken)
    {
        var professional = Professional.Create(
            request.Name, request.Email, request.Phone,
            request.Specialty, request.Bio, request.AvatarUrl, request.UserId);

        professionalRepository.Add(professional);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(professional.Id);
    }
}
