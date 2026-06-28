using System.Text;
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Reports.Queries;
using Horafy.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
public sealed class ReportsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("reports/revenue")]
    [ProducesResponseType(typeof(RevenueReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to   = null,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetRevenueReportQuery(from, to), cancellationToken));

    [HttpGet("reports/revenue/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportRevenueCsv(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to   = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetRevenueReportQuery(from, to), cancellationToken);
        if (!result.IsSuccess) return ToActionResult(result);

        var report = result.Value;
        var csv    = BuildCsv(report);
        var bytes  = Encoding.UTF8.GetBytes(csv);
        var name   = $"receita_{report.From:yyyy-MM-dd}_{report.To:yyyy-MM-dd}.csv";

        return File(bytes, "text/csv", name);
    }

    [HttpGet("reports/customers")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerExportRecord>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomers(CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetCustomersReportQuery(), cancellationToken));

    [HttpGet("reports/customers/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportCustomersCsv(CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCustomersReportQuery(), cancellationToken);
        if (!result.IsSuccess) return ToActionResult(result);

        var csv   = BuildCustomersCsv(result.Value);
        var bytes = Encoding.UTF8.GetBytes(csv);
        var name  = $"clientes_{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}.csv";

        return File(bytes, "text/csv", name);
    }

    private static string BuildCustomersCsv(IReadOnlyList<CustomerExportRecord> customers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nome,E-mail,Telefone,Agendamentos,Último agendamento,Total gasto (R$)");
        foreach (var c in customers)
            sb.AppendLine(string.Join(',',
                Csv(c.Name),
                Csv(c.Email),
                Csv(c.Phone ?? ""),
                c.BookingCount,
                c.LastBookingAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
                c.TotalSpent.ToString("F2")));
        return sb.ToString();
    }

    /// <summary>Escapa um campo CSV (aspas e separadores) conforme RFC 4180.</summary>
    private static string Csv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static string BuildCsv(RevenueReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Relatório de Receita,{report.From:dd/MM/yyyy},{report.To:dd/MM/yyyy}");
        sb.AppendLine($"Total de Receita,{report.TotalRevenue:F2}");
        sb.AppendLine($"Pagamentos Aprovados,{report.ApprovedPaymentsCount}");
        sb.AppendLine();

        sb.AppendLine("Por Dia,Receita (R$),Pagamentos");
        foreach (var d in report.ByDay)
            sb.AppendLine($"{d.Date:dd/MM/yyyy},{d.Revenue:F2},{d.Count}");

        sb.AppendLine();
        sb.AppendLine("Por Serviço,Agendamentos,Receita (R$)");
        foreach (var s in report.ByService)
            sb.AppendLine($"{s.ServiceName},{s.BookingCount},{s.Revenue:F2}");

        return sb.ToString();
    }
}
