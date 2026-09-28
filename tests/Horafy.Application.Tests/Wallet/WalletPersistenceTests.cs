using FluentAssertions;
using Horafy.Application.Features.Wallet.Commands.AddCredits;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;

namespace Horafy.Application.Tests.Wallet;

/// <summary>
/// Movimento de carteira gravado de verdade no Postgres. O <c>Id</c> da
/// <c>WalletTransaction</c> nasce no construtor (<c>BaseEntity</c>); se o EF a toma
/// por existente, emite UPDATE em vez de INSERT e o SaveChanges estoura com 0
/// linhas afetadas — a mesma armadilha que derrubou o upload de fotos em
/// 26/09/2026 (ver <c>RentableItemImagePersistenceTests</c>). Os testes de
/// handler usam mocks e não enxergam isso. Requer Docker (Postgres real).
/// </summary>
public sealed class WalletPersistenceTests : IAsyncLifetime
{
    private const string Slug   = "wallettest";
    private const string Schema = "tenant_" + Slug;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var global = NewGlobalContext();
        var schemaService = new TenantSchemaService(
            global, new ConfigurationBuilder().Build(),
            NullLogger<TenantSchemaService>.Instance);
        await schemaService.CreateSchemaAsync(Slug);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task AddCredits_FirstCredit_CreatesWalletAndTransaction()
    {
        var userId = Guid.NewGuid();

        var result = await AddCredits(userId, 50m);

        result.IsSuccess.Should().BeTrue();
        var wallet = await LoadWallet(userId);
        wallet.Balance.Should().Be(50m);
        wallet.Transactions.Should().ContainSingle();
    }

    [Fact]
    public async Task AddCredits_ToExistingWallet_AppendsTransaction()
    {
        var userId = Guid.NewGuid();
        (await AddCredits(userId, 50m)).IsSuccess.Should().BeTrue();

        var result = await AddCredits(userId, 30m);

        result.IsSuccess.Should().BeTrue();
        var wallet = await LoadWallet(userId);
        wallet.Balance.Should().Be(80m);
        wallet.Transactions.Should().HaveCount(2);
    }

    /// <summary>
    /// Débito (CreatePaymentCommand) e estorno de locação (MarkRentalReturnedCommand)
    /// mexem numa carteira já salva do mesmo jeito: carrega, altera, Update, salva.
    /// </summary>
    [Fact]
    public async Task DebitAndRefund_OnExistingWallet_AppendTransactions()
    {
        var userId    = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        (await AddCredits(userId, 100m)).IsSuccess.Should().BeTrue();

        await using (var ctx = NewTenantContext())
        {
            var repo   = new WalletRepository(ctx);
            var wallet = await repo.GetByUserIdAsync(userId);
            wallet!.DebitPayment(40m, "Agendamento", bookingId).IsSuccess.Should().BeTrue();
            wallet.RefundFromBooking(15m, "Caução", bookingId).IsSuccess.Should().BeTrue();
            repo.Update(wallet); // como os dois handlers fazem

            var save = () => new TenantUnitOfWork(ctx).SaveChangesAsync();
            await save.Should().NotThrowAsync();
        }

        var saved = await LoadWallet(userId);
        saved.Balance.Should().Be(75m);
        saved.Transactions.Should().HaveCount(3);
    }

    // ── Wiring (mesmo de RentalStockConcurrencyTests) ─────────────────────────────

    private async Task<Horafy.Shared.Result> AddCredits(Guid userId, decimal amount)
    {
        await using var ctx = NewTenantContext();
        var handler = new AddCreditsCommandHandler(new WalletRepository(ctx), new TenantUnitOfWork(ctx));
        return await handler.Handle(new AddCreditsCommand(userId, amount, "Crédito manual"), default);
    }

    private async Task<WalletEntity> LoadWallet(Guid userId)
    {
        await using var ctx = NewTenantContext();
        var wallet = await new WalletRepository(ctx).GetByUserIdAsync(userId);
        wallet.Should().NotBeNull();
        return wallet!;
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
