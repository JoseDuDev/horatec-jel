using FluentValidation;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

/// <summary>
/// Retorna os dias (no intervalo [From, To]) que têm ao menos um horário livre para o
/// recurso/serviço. Evita o cliente (bot) varrer dia a dia. Reusa a lógica de slots.
/// </summary>
public sealed record GetAvailableDaysQuery(
    Guid ResourceId,
    DateOnly From,
    DateOnly To,
    Guid? ServiceId) : IRequest<Result<IReadOnlyList<DateOnly>>>;

public sealed class GetAvailableDaysQueryValidator : AbstractValidator<GetAvailableDaysQuery>
{
    public const int MaxRangeDays = 31;

    public GetAvailableDaysQueryValidator()
    {
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("'to' deve ser maior ou igual a 'from'.");
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber <= MaxRangeDays)
            .WithMessage($"O intervalo não pode exceder {MaxRangeDays} dias.");
    }
}

internal sealed class GetAvailableDaysQueryHandler(ISender sender)
    : IRequestHandler<GetAvailableDaysQuery, Result<IReadOnlyList<DateOnly>>>
{
    public async Task<Result<IReadOnlyList<DateOnly>>> Handle(
        GetAvailableDaysQuery request, CancellationToken cancellationToken)
    {
        var days = new List<DateOnly>();

        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            var slots = await sender.Send(
                new GetAvailableSlotsQuery(request.ResourceId, date, request.ServiceId),
                cancellationToken);

            // Falha numa query individual não derruba a varredura: apenas pula o dia.
            if (slots.IsSuccess && slots.Value.Count > 0)
                days.Add(date);
        }

        return Result.Success<IReadOnlyList<DateOnly>>(days);
    }
}
