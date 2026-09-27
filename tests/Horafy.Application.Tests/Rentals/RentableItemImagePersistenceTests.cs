using FluentAssertions;
using Horafy.Domain.Entities.Rentals;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Horafy.Application.Tests.Rentals;

/// <summary>
/// Foto nova num item que JÁ existe no banco — o caminho do upload
/// (<c>AddRentableItemImageCommand</c>): carrega o item com a galeria, chama
/// <c>AddImage</c> e salva. Os testes do handler usam mocks e não pegam o que
/// quebrou em produção em 26/09/2026: o <c>Id</c> nasce preenchido no construtor
/// (<c>BaseEntity</c>), o EF tomava a foto por existente, emitia UPDATE em vez de
/// INSERT e o SaveChanges estourava (0 linhas afetadas) — 500 com o arquivo já no
/// disco. Requer Docker (Postgres real).
/// </summary>
public sealed class RentableItemImagePersistenceTests : IAsyncLifetime
{
    private const string Slug   = "imgtest";
    private const string Schema = "tenant_" + Slug;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private Guid _itemId;

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

        // Item salvo SEM foto, como o admin faz: cria primeiro, envia as fotos depois.
        await using var ctx = NewTenantContext();
        var item = RentableItem.Create("Pula-pula", quantity: 1, dailyRate: 50m);
        ctx.RentableItems.Add(item);
        await ctx.SaveChangesAsync();
        _itemId = item.Id;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task AddImage_ToExistingItem_InsertsPhotoAndSetsCover()
    {
        // Contexto novo, como numa requisição: o item vem do banco, a foto não.
        await using (var ctx = NewTenantContext())
        {
            var item = await new RentableItemRepository(ctx).GetByIdWithImagesAsync(_itemId);
            item!.AddImage("https://api/uploads/imgtest/itens/202609/a.jpg");
            item.AddImage("https://api/uploads/imgtest/itens/202609/b.jpg");

            var save = () => ctx.SaveChangesAsync();
            await save.Should().NotThrowAsync();
        }

        await using var verifyCtx = NewTenantContext();
        var saved = await new RentableItemRepository(verifyCtx).GetByIdWithImagesAsync(_itemId);

        saved!.Images.Should().HaveCount(2);
        saved.ImageUrl.Should().Be("https://api/uploads/imgtest/itens/202609/a.jpg");
    }

    // ── Wiring (mesmo de RentalStockConcurrencyTests) ─────────────────────────────

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
