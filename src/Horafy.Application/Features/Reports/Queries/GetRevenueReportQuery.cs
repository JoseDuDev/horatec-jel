using Horafy.Application.Interfaces;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Reports.Queries;

public sealed record GetRevenueReportQuery(
    DateOnly? From = null,
    DateOnly? To   = null) : IRequest<Result<RevenueReport>>;

internal sealed class GetRevenueReportQueryHandler(IRevenueReportReader reader)
    : IRequestHandler<GetRevenueReportQuery, Result<RevenueReport>>
{
    public async Task<Result<RevenueReport>> Handle(
        GetRevenueReportQuery request, CancellationToken ct)
    {
        var to   = request.To   ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? to.AddDays(-29);

        var report = await reader.GetReportAsync(from, to, ct);
        return Result<RevenueReport>.Success(report);
    }
}
