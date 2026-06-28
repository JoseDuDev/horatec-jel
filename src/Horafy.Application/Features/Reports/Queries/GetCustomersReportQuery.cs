using Horafy.Application.Interfaces;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Reports.Queries;

/// <summary>Carteira de clientes do tenant (para exibição e exportação CSV).</summary>
public sealed record GetCustomersReportQuery
    : IRequest<Result<IReadOnlyList<CustomerExportRecord>>>;

internal sealed class GetCustomersReportQueryHandler(ICustomerListReader reader)
    : IRequestHandler<GetCustomersReportQuery, Result<IReadOnlyList<CustomerExportRecord>>>
{
    public async Task<Result<IReadOnlyList<CustomerExportRecord>>> Handle(
        GetCustomersReportQuery request, CancellationToken ct)
    {
        var customers = await reader.GetCustomersAsync(ct);
        return Result.Success(customers);
    }
}
