using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Availability.Queries;

public sealed record GetResourceRulesQuery(Guid ResourceId)
    : IRequest<Result<IReadOnlyList<AvailabilityRuleResult>>>;

public sealed record AvailabilityRuleResult(
    Guid Id,
    Guid ResourceId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes,
    int BreakAfterMinutes);

internal sealed class GetResourceRulesQueryHandler(
    IAvailabilityRepository repository)
    : IRequestHandler<GetResourceRulesQuery, Result<IReadOnlyList<AvailabilityRuleResult>>>
{
    public async Task<Result<IReadOnlyList<AvailabilityRuleResult>>> Handle(
        GetResourceRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await repository.GetRulesByResourceAsync(request.ResourceId, cancellationToken);

        var result = rules.Select(r => new AvailabilityRuleResult(
            r.Id, r.ResourceId, r.DayOfWeek,
            r.StartTime, r.EndTime,
            r.SlotDurationMinutes, r.BreakAfterMinutes)).ToList();

        return Result.Success<IReadOnlyList<AvailabilityRuleResult>>(result);
    }
}
