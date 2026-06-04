using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Tenants.Commands.UpdateTenant;

public sealed record UpdateTenantCommand(
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string? TimeZoneId,
    string? Locale) : IRequest<Result>;

public sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.Phone).MaximumLength(20);
    }
}

internal sealed class UpdateTenantCommandHandler(
    ICurrentTenantService currentTenant,
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateTenantCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue)
            return Result.Failure(Error.Unauthorized);

        var tenant = await tenantRepository.GetByIdAsync(
            currentTenant.TenantId.Value, cancellationToken);

        if (tenant is null) return Result.Failure(TenantErrors.NotFound);

        tenant.UpdateInfo(
            request.Name, request.Email, request.Phone,
            request.Address, request.City, request.State, request.ZipCode,
            request.TimeZoneId, request.Locale);

        tenantRepository.Update(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
