using FluentAssertions;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
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
/// Paridade SQL do <see cref="DashboardReader"/> contra um Postgres real (Testcontainers).
/// A agregação foi empurrada para o SQL; este teste prova que as queries TRADUZEM e que os
/// números batem com um seed conhecido (o seed é o oráculo). O provedor InMemory não valida
/// este SQL (GroupBy + SelectMany + DateOnly.FromDateTime).
/// </summary>
public sealed class DashboardReaderSqlTests : IAsyncLifetime
{
    private const string Slug   = "dashtest";
    private const string Schema = "tenant_" + Slug;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    // Janela futura (Booking.Create exige ScheduledAt futuro) e estável contra o relógio real.
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

        // Linhas-pai para as FKs. Os agrupamentos do reader usam os snapshots
        // (service_name em booking_services, resource_name em bookings), não estas FKs,
        // então uma única linha de cada serve para todas as reservas.
        var service  = Service.Create("Serviço", durationMinutes: 30, price: 0m);
        var resource = Resource.Create("Recurso", ResourceType.Professional);
        ctx.Services.Add(service);
        ctx.Resources.Add(resource);
        await ctx.SaveChangesAsync();
        _serviceId  = service.Id;
        _resourceId = resource.Id;

        // Day1: dois confirmados na Room A.
        ctx.Bookings.Add(Appt(_day1, "Room A", BookingStatus.Confirmed, ("Corte", 30m), ("Barba", 20m)));
        ctx.Bookings.Add(Appt(_day1, "Room A", BookingStatus.Confirmed, ("Corte", 30m)));
        // Day2: um cancelado (Room B) e um no-show (Room A).
        ctx.Bookings.Add(Appt(Day2, "Room B", BookingStatus.Cancelled, ("Corte", 30m), ("Massagem", 100m)));
        ctx.Bookings.Add(Appt(Day2, "Room A", BookingStatus.NoShow, ("Barba", 20m)));
        // Day3: um pendente (Room B).
        ctx.Bookings.Add(Appt(Day3, "Room B", BookingStatus.Pending, ("Massagem", 100m), ("Corte", 30m)));

        await ctx.SaveChangesAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task GetStats_AggregatesInSql_MatchesSeededOracle()
    {
        await using var ctx = NewTenantContext();
        var reader = new DashboardReader(ctx);

        var stats = await reader.GetStatsAsync(_day1, Day3, default);

        // Contagens por status.
        stats.TotalBookings.Should().Be(5);
        stats.ConfirmedBookings.Should().Be(2);
        stats.CancelledBookings.Should().Be(1);
        stats.NoShowBookings.Should().Be(1);
        stats.CancellationRate.Should().Be(20.0m); // 1/5

        // Receita exclui Cancelled E NoShow: B1(50) + B2(30) + B5pendente(130) = 210.
        stats.TotalRevenue.Should().Be(210m);

        // Top serviços (exclui só Cancelled), ordem por contagem desc.
        stats.TopServices.Should().HaveCount(3);
        stats.TopServices[0].Should().BeEquivalentTo(new { ServiceName = "Corte",    BookingCount = 3, Revenue = 90m });
        stats.TopServices[1].Should().BeEquivalentTo(new { ServiceName = "Barba",    BookingCount = 2, Revenue = 40m });
        stats.TopServices[2].Should().BeEquivalentTo(new { ServiceName = "Massagem", BookingCount = 1, Revenue = 100m });

        // Top recursos (exclui Cancelled e nomes vazios), ordem por contagem desc.
        stats.TopResources.Should().HaveCount(2);
        stats.TopResources[0].Should().BeEquivalentTo(new { ResourceName = "Room A", BookingCount = 3 });
        stats.TopResources[1].Should().BeEquivalentTo(new { ResourceName = "Room B", BookingCount = 1 });

        // Reservas por dia (todos os status), ordem crescente.
        stats.BookingsByDay.Should().HaveCount(3);
        stats.BookingsByDay[0].Should().Be(new DailyBookingItem(_day1, 2));
        stats.BookingsByDay[1].Should().Be(new DailyBookingItem(Day2, 2));
        stats.BookingsByDay[2].Should().Be(new DailyBookingItem(Day3, 1));
    }

    private Booking Appt(
        DateOnly day, string resource, BookingStatus target, params (string Name, decimal Price)[] services)
    {
        // services[0].ServiceId vira booking.ServiceId (FK) → usa o serviço real.
        var svc = services.Select(s => (_serviceId, s.Name, 30, s.Price)).ToArray();
        var b = Booking.Create(
            svc,
            resourceId:   _resourceId,
            resourceName: resource,
            customerId:   Guid.NewGuid(),
            customerName: "Cliente",
            customerEmail: "c@test.com",
            scheduledAt:  new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero));

        switch (target)
        {
            case BookingStatus.Confirmed: b.Confirm(); break;
            case BookingStatus.Cancelled: b.Cancel("motivo"); break;
            case BookingStatus.NoShow:    b.Confirm(); b.MarkNoShow(); break;
        }

        return b;
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
