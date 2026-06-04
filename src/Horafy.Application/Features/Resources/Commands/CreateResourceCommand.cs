using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record CreateResourceCommand(
    string Name,
    ResourceType Type,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    string? AvatarUrl,
    Guid? UserId) : IRequest<Result<Guid>>;

public sealed class CreateResourceCommandValidator : AbstractValidator<CreateResourceCommand>
{
    public CreateResourceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
    }
}

internal sealed class CreateResourceCommandHandler(
    IResourceRepository resourceRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreateResourceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateResourceCommand request, CancellationToken cancellationToken)
    {
        var resource = Resource.Create(
            request.Name, request.Type, request.Email, request.Phone,
            request.Specialty, request.Bio, request.AvatarUrl, request.UserId);

        resourceRepository.Add(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(resource.Id);
    }
}
