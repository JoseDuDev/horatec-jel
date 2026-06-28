using Horafy.Application.Interfaces;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Dashboard.Queries;

public sealed record GetDashboardQuery(
    DateOnly? From = null,
    DateOnly? To   = null) : IRequest<Result<DashboardStats>>;

internal sealed class GetDashboardQueryHandler(IDashboardReader reader)
    : IRequestHandler<GetDashboardQuery, Result<DashboardStats>>
{
    public async Task<Result<DashboardStats>> Handle(
        GetDashboardQuery request, CancellationToken ct)
    {
        var to   = request.To   ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? to.AddDays(-29);

        var stats = await reader.GetStatsAsync(from, to, ct);
        return Result<DashboardStats>.Success(stats);
    }
}
