using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Resources.Commands;

public sealed record UpdateResourceCommand(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    string? AvatarUrl) : IRequest<Result>;

public sealed class UpdateResourceCommandValidator : AbstractValidator<UpdateResourceCommand>
{
    public UpdateResourceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
    }
}

internal sealed class UpdateResourceCommandHandler(
    IResourceRepository resourceRepository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<UpdateResourceCommand, Result>
{
    public async Task<Result> Handle(
        UpdateResourceCommand request, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (resource is null) return Result.Failure(ResourceErrors.NotFound);

        resource.Update(request.Name, request.Email, request.Phone,
            request.Specialty, request.Bio, request.AvatarUrl);

        resourceRepository.Update(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
