using FluentAssertions;
using Horafy.Application.Features.Wallet.Commands.AddCredits;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Rentals;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Persistence.Interceptors;
using Horafy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;

namespace Horafy.Application.Tests.Persistence;

/// <summary>
/// Auditoria nas linhas do schema do tenant, com o contexto montado por
/// <see cref="TenantDbContext.BuildOptions"/> — o mesmo caminho do DI de produção.
/// Até 28/09/2026 o DI montava o TenantDbContext sem o interceptor de auditoria e
/// tudo saía com created_at 0001-01-01. O teste cobre também o filho novo achado só
/// na coleção do agregado (foto, transação de carteira). Requer Docker (Postgres real).
/// </summary>
public sealed class TenantAuditTests : IAsyncLifetime
{
    private const string Slug   = "audittest";
    private const string Schema = "tenant_" + Slug;

    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var global = new HorafyDbContext(new DbContextOptionsBuilder<HorafyDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options);
        await using (global)
        {
            var schemaService = new TenantSchemaService(
                global, new ConfigurationBuilder().Build(),
                NullLogger<TenantSchemaService>.Instance);
            await schemaService.CreateSchemaAsync(Slug);
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task NewItem_And_PhotoAddedLater_GetCreatedAtAndCreatedBy()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        Guid itemId;

        await using (var ctx = NewTenantContext())
        {
            var item = RentableItem.Create("Pula-pula", quantity: 1, dailyRate: 50m);
            ctx.RentableItems.Add(item);
            await ctx.SaveChangesAsync();
            itemId = item.Id;
        }

        // Foto nova só na coleção do item, sem Add explícito.
        await using (var ctx = NewTenantContext())
        {
            var item = await new RentableItemRepository(ctx).GetByIdWithImagesAsync(itemId);
            item!.AddImage("https://api/uploads/audittest/itens/202609/a.jpg");
            await ctx.SaveChangesAsync();
        }

        await using var verify = NewTenantContext();
        var saved = await new RentableItemRepository(verify).GetByIdWithImagesAsync(itemId);

        saved!.CreatedAt.Should().BeAfter(before);
        saved.CreatedBy.Should().Be(TenantId.ToString());
        saved.Images.Should().ContainSingle()
            .Which.CreatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task WalletTransactions_GetCreatedAt()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        var userId = Guid.NewGuid();

        await AddCredits(userId, 10m); // carteira nova
        await AddCredits(userId, 5m);  // carteira existente: transação achada pela coleção

        await using var verify = NewTenantContext();
        var wallet = await new WalletRepository(verify).GetByUserIdAsync(userId);

        wallet!.CreatedAt.Should().BeAfter(before);
        wallet.Transactions.Should().HaveCount(2)
            .And.OnlyContain(t => t.CreatedAt > before);
    }

    // ── Wiring ──────────────────────────────────────────────────────────────────

    private async Task AddCredits(Guid userId, decimal amount)
    {
        await using var ctx = NewTenantContext();
        var handler = new AddCreditsCommandHandler(new WalletRepository(ctx), new TenantUnitOfWork(ctx));
        var result  = await handler.Handle(new AddCreditsCommand(userId, amount, "Crédito"), default);
        result.IsSuccess.Should().BeTrue();
    }

    private TenantDbContext NewTenantContext()
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);

        var options = TenantDbContext.BuildOptions(
            _container.GetConnectionString(),
            $"{Schema},public",
            new AuditableEntityInterceptor(tenant.Object));

        return new TenantDbContext(options);
    }
}
