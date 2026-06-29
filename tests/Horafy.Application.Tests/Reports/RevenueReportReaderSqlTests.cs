using FluentAssertions;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Horafy.Application.Tests.Reports;

/// <summary>
/// Paridade SQL do <see cref="RevenueReportReader"/> contra um Postgres real (Testcontainers).
/// Total/contagem/byDay agregam direto sobre Payment; byService junta pagamentos→reservas→serviços
/// no SQL (subquery DISTINCT de booking ids). O seed conhecido é o oráculo. CreatedAt dos pagamentos
/// é definido manualmente (sem interceptor neste contexto de teste) para cair na janela escolhida.
/// </summary>
public sealed class RevenueReportReaderSqlTests : IAsyncLifetime
{
    private const string Slug   = "revtest";
    private const string Schema = "tenant_" + Slug;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly DateOnly _day1 = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(10);
    private DateOnly Day2 => _day1.AddDays(1);
    private DateOnly Day3 => _day1.AddDays(2);

    // Service/Resource reais para satisfazer as FKs bookings.service_id/resource_id.
    private Guid _serviceId;
    private Guid _resourceId;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var global = NewGlobalContext())
        {
            var schemaService = new TenantSchemaService(
                global, new ConfigurationBuilder().Build(),
                NullLogger<TenantSchemaService>.Instance);
            await schemaService.CreateSchemaAsync(Slug);
        }

        await using var ctx = NewTenantContext();

        var service  = Service.Create("Serviço", durationMinutes: 30, price: 0m);
        var resource = Resource.Create("Recurso", ResourceType.Professional);
        ctx.Services.Add(service);
        ctx.Resources.Add(resource);
        await ctx.SaveChangesAsync();
        _serviceId  = service.Id;
        _resourceId = resource.Id;

        // Reservas referenciadas pelos pagamentos (ScheduledAt futuro; data não importa para o reader).
        var bookingA = Booking2Svc(("Corte", 30m), ("Barba", 20m));
        var bookingB = Booking1Svc("Massagem", 100m);
        var bookingC = Booking1Svc("Corte", 30m);
        ctx.Bookings.AddRange(bookingA, bookingB, bookingC);

        // Aprovados DENTRO da janela [Day1, Day2].
        ctx.Payments.Add(MakePayment(bookingA.Id, amount: 50m,  approved: true,  _day1));
        ctx.Payments.Add(MakePayment(bookingB.Id, amount: 100m, approved: true,  _day1));
        ctx.Payments.Add(MakePayment(bookingC.Id, amount: 30m,  approved: true,  Day2));
        // Pendente (não aprovado) → excluído.
        ctx.Payments.Add(MakePayment(bookingA.Id, amount: 999m, approved: false, _day1));
        // Aprovado mas FORA da janela (Day3 > to) → excluído.
        ctx.Payments.Add(MakePayment(bookingB.Id, amount: 200m, approved: true,  Day3));

        await ctx.SaveChangesAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task GetReport_AggregatesInSql_MatchesSeededOracle()
    {
        await using var ctx = NewTenantContext();
        var reader = new RevenueReportReader(ctx);

        var report = await reader.GetReportAsync(_day1, Day2, default);

        // Total/contagem: só aprovados na janela = 50 + 100 + 30.
        report.TotalRevenue.Should().Be(180m);
        report.ApprovedPaymentsCount.Should().Be(3);

        // Por dia, ordem crescente.
        report.ByDay.Should().HaveCount(2);
        report.ByDay[0].Should().Be(new DailyRevenueItem(_day1, 150m, 2)); // 50 + 100
        report.ByDay[1].Should().Be(new DailyRevenueItem(Day2, 30m, 1));

        // Por serviço, ordem por receita desc (booking ids distintos dos aprovados na janela).
        report.ByService.Should().HaveCount(3);
        report.ByService[0].Should().BeEquivalentTo(new { ServiceName = "Massagem", BookingCount = 1, Revenue = 100m });
        report.ByService[1].Should().BeEquivalentTo(new { ServiceName = "Corte",    BookingCount = 2, Revenue = 60m });
        report.ByService[2].Should().BeEquivalentTo(new { ServiceName = "Barba",    BookingCount = 1, Revenue = 20m });
    }

    private Booking Booking2Svc((string Name, decimal Price) s1, (string Name, decimal Price) s2) =>
        Booking.Create(
            new[] { (_serviceId, s1.Name, 30, s1.Price), (_serviceId, s2.Name, 30, s2.Price) },
            resourceId: _resourceId, resourceName: "Room", customerId: Guid.NewGuid(),
            customerName: "Cliente", customerEmail: "c@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddDays(5));

    private Booking Booking1Svc(string name, decimal price) =>
        Booking.Create(
            new[] { (_serviceId, name, 30, price) },
            resourceId: _resourceId, resourceName: "Room", customerId: Guid.NewGuid(),
            customerName: "Cliente", customerEmail: "c@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddDays(5));

    private static Payment MakePayment(Guid bookingId, decimal amount, bool approved, DateOnly day)
    {
        var p = Payment.Create(
            bookingId, preferenceId: "pref", method: PaymentMethod.Pix,
            amount: amount, depositAmount: 0m);
        if (approved) p.Approve("mp-" + Guid.NewGuid());
        // Sem interceptor neste contexto: CreatedAt é controlado manualmente para a janela.
        p.CreatedAt = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
        return p;
    }

    private HorafyDbContext NewGlobalContext()
    {
        var options = new DbContextOptionsBuilder<HorafyDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new HorafyDbContext(options);
    }

    private TenantDbContext NewTenantContext()
    {
        var conn = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            SearchPath = $"{Schema},public"
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(conn, npgsql => npgsql.SetPostgresVersion(16, 0))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenantDbContext(options);
    }
}
