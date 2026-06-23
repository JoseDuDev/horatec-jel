using FluentAssertions;
using Horafy.Application.Features.Rentals;
using Horafy.Application.Features.Rentals.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories;
using Horafy.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Horafy.Application.Tests.Rentals;

/// <summary>
/// Teste de concorrência de estoque (Fase 5): valida que a transação Serializable do
/// <see cref="CreateRentalBookingCommandHandler"/> impede overbooking. Duas reservas
/// simultâneas do mesmo item com estoque 1 devem resultar em exatamente uma confirmada
/// e uma <see cref="RentalErrors.OutOfStock"/> — nunca duas. Requer Docker (Postgres real;
/// o provedor InMemory não suporta transações/isolamento Serializable).
/// </summary>
public sealed class RentalStockConcurrencyTests : IAsyncLifetime
{
    private const string Slug   = "ctest";
    private const string Schema = "tenant_" + Slug;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private Guid _itemId;

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

        // Semeia um item com estoque 1 (uma única unidade física disponível).
        await using var ctx = NewTenantContext();
        var item = RentableItem.Create("Furadeira de Impacto", quantity: 1, dailyRate: 30m);
        ctx.RentableItems.Add(item);
        await ctx.SaveChangesAsync();
        _itemId = item.Id;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task TwoConcurrentReserves_SingleUnitStock_OnlyOneSucceeds()
    {
        var customerId = Guid.NewGuid();
        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(1);
        var end   = start.AddDays(3);

        // Cada handler opera com seu PRÓPRIO contexto/conexão — concorrência real.
        async Task<Result<Guid>> Reserve()
        {
            await using var ctx = NewTenantContext();
            var handler = NewHandler(ctx, customerId);
            var command = new CreateRentalBookingCommand(
                new[] { new RentalItemLine(_itemId, Quantity: 1) }, start, end, Notes: null);
            return await handler.Handle(command, default);
        }

        var results = await Task.WhenAll(Reserve(), Reserve());

        // Invariante anti-overbooking: exatamente uma confirmada, uma sem estoque.
        results.Count(r => r.IsSuccess).Should().Be(1);
        var failure = results.Single(r => r.IsFailure);
        failure.Error.Should().Be(RentalErrors.OutOfStock);

        // O estoque (1 unidade) reflete uma única reserva ativa no período.
        await using var verifyCtx = NewTenantContext();
        var reserved = await new BookingRepository(verifyCtx).CountReservedUnitsAsync(
            _itemId,
            new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            new DateTimeOffset(end.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));
        reserved.Should().Be(1);
    }

    // ── Wiring ──────────────────────────────────────────────────────────────────

    private CreateRentalBookingCommandHandler NewHandler(TenantDbContext ctx, Guid customerId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        currentUser.SetupGet(u => u.UserId).Returns(customerId);
        currentUser.SetupGet(u => u.Email).Returns("c@test.com");
        currentUser.SetupGet(u => u.Role).Returns(UserRole.Customer);

        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((User?)null);

        return new CreateRentalBookingCommandHandler(
            new RentableItemRepository(ctx),
            new BookingRepository(ctx),
            users.Object,
            currentUser.Object,
            new TenantUnitOfWork(ctx));
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
            .UseNpgsql(conn, npgsql =>
            {
                npgsql.SetPostgresVersion(16, 0);
                // Retry on failure → a estratégia de execução re-tenta a falha de
                // serialização (40001) do SSI; o perdedor relê o estoque e retorna OutOfStock.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(2), null);
            })
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenantDbContext(options);
    }
}
