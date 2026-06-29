using FluentAssertions;
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
/// Teste de paridade do <see cref="CustomerListReader"/> agregado em SQL contra um Postgres
/// real (Testcontainers; o provedor InMemory não valida a tradução das três consultas).
/// Fixa a semântica documentada: BookingCount inclui cancelados; TotalSpent EXCLUI cancelados;
/// nome/contato vêm do agendamento mais recente; ordenação por nome ascendente.
/// </summary>
public sealed class CustomerListReaderTests : IAsyncLifetime
{
    private const string Slug   = "creport";
    private const string Schema = "tenant_" + Slug;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private DateTimeOffset _customerALast;
    private Guid           _serviceId;
    private Guid           _resourceId;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Provisiona o schema do tenant reutilizando o DDL real de produção.
        await using (var global = NewGlobalContext())
        {
            var schemaService = new TenantSchemaService(
                global, new ConfigurationBuilder().Build(),
                NullLogger<TenantSchemaService>.Instance);
            await schemaService.CreateSchemaAsync(Slug);
        }

        await using var ctx = NewTenantContext();

        // bookings.service_id / resource_id têm FK para services/resources — semeia linhas reais.
        var service  = Service.Create("Corte", durationMinutes: 30, price: 0m);
        var resource = Resource.Create("Recurso", ResourceType.Professional);
        ctx.Services.Add(service);
        ctx.Resources.Add(resource);
        await ctx.SaveChangesAsync();
        _serviceId  = service.Id;
        _resourceId = resource.Id;

        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var now       = DateTimeOffset.UtcNow;

        // Cliente A: 2 bookings NÃO cancelados (50 e 30) + 1 cancelado (100).
        // O booking mais recente (max ScheduledAt) é o cancelado e carrega "Ana B".
        var a1 = NewBooking(customerA, "Ana Old", "ana.old@test.com", "111", now.AddDays(1), 50m);
        var a2 = NewBooking(customerA, "Ana Mid", "ana.mid@test.com", "222", now.AddDays(2), 30m);
        var a3 = NewBooking(customerA, "Ana B",   "ana.b@test.com",   "333", now.AddDays(3), 100m);
        a3.Cancel("desistiu");
        _customerALast = a3.ScheduledAt;

        // Cliente B: somente 1 booking cancelado (40) → TotalSpent = 0.
        var b1 = NewBooking(customerB, "Bruno", "bruno@test.com", "999", now.AddDays(1), 40m);
        b1.Cancel("desistiu");

        ctx.Bookings.AddRange(a1, a2, a3, b1);
        await ctx.SaveChangesAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task GetCustomersAsync_AggregatesPerCustomer_WithDocumentedSemantics()
    {
        await using var ctx = NewTenantContext();
        var reader = new CustomerListReader(ctx);

        var result = await reader.GetCustomersAsync();

        // Dois clientes, ordenados por nome ascendente: "Ana B" < "Bruno".
        result.Should().HaveCount(2);
        result.Select(c => c.Name).Should().ContainInOrder("Ana B", "Bruno");

        var ana = result[0];
        ana.Name.Should().Be("Ana B");                       // contato do booking mais recente
        ana.Email.Should().Be("ana.b@test.com");
        ana.Phone.Should().Be("333");
        ana.BookingCount.Should().Be(3);                     // inclui o cancelado
        ana.TotalSpent.Should().Be(80m);                     // 50 + 30; EXCLUI o cancelado (100)
        // Postgres TIMESTAMPTZ trunca para microssegundos; o DateTimeOffset .NET tem 100ns.
        // 1ms de tolerância absorve o truncamento e ainda distingue o mais recente (dias à parte).
        ana.LastBookingAt.Should().BeCloseTo(_customerALast, TimeSpan.FromMilliseconds(1));

        var bruno = result[1];
        bruno.Name.Should().Be("Bruno");
        bruno.Email.Should().Be("bruno@test.com");
        bruno.Phone.Should().Be("999");
        bruno.BookingCount.Should().Be(1);
        bruno.TotalSpent.Should().Be(0m);                    // único booking é cancelado
    }

    // ── Wiring ──────────────────────────────────────────────────────────────────

    private Booking NewBooking(
        Guid customerId, string name, string email, string phone,
        DateTimeOffset scheduledAt, decimal price) =>
        Booking.Create(
            new[] { (_serviceId, "Serviço", 30, price) },
            resourceId:    _resourceId,
            resourceName:  "Recurso",
            customerId:    customerId,
            customerName:  name,
            customerEmail: email,
            scheduledAt:   scheduledAt,
            customerPhone: phone);

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
