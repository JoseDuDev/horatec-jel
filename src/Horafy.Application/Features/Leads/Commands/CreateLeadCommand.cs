using FluentValidation;
using Horafy.Domain.Entities.Leads;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Leads.Commands;

/// <summary>
/// Registra um lead de interesse vindo da landing pública. Endpoint anônimo:
/// validação estrita e deduplicação por telefone para conter spam/reenvio.
/// </summary>
public sealed record CreateLeadCommand(
    string  Name,
    string  Phone,
    string? Email,
    string? BusinessType,
    string? Message,
    string  Brand) : IRequest<Result<Guid>>;

public sealed class CreateLeadCommandValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(@"^[\d\s()+.-]+$").WithMessage("Telefone deve conter apenas dígitos e separadores.")
            .Must(p => (p ?? string.Empty).Count(char.IsDigit) >= 10)
            .WithMessage("Telefone deve ter ao menos 10 dígitos (DDD + número).");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.BusinessType).MaximumLength(100);
        RuleFor(x => x.Message).MaximumLength(1000);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(50);
    }
}

internal sealed class CreateLeadCommandHandler(
    IPlatformLeadRepository leadRepository,
    IUnitOfWork             unitOfWork)
    : IRequestHandler<CreateLeadCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateLeadCommand request, CancellationToken cancellationToken)
    {
        var phone = request.Phone.Trim();

        // Reenvio nas últimas 24h (duplo clique, refresh) responde sucesso sem
        // inserir de novo — o interessado não precisa saber que já estava na fila.
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        if (await leadRepository.RecentPhoneExistsAsync(phone, since, cancellationToken))
        {
            return Result<Guid>.Success(Guid.Empty);
        }

        var lead = PlatformLead.Create(
            request.Name,
            phone,
            request.Email,
            request.BusinessType,
            request.Message,
            request.Brand);

        leadRepository.Add(lead);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(lead.Id);
    }
}
