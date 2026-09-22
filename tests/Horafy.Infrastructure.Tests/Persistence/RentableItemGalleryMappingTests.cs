using FluentAssertions;
using Horafy.Domain.Entities.Rentals;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Horafy.Infrastructure.Tests.Persistence;

/// <summary>
/// A galeria é exposta por uma propriedade só-leitura (<c>Images</c>) sobre o campo
/// <c>_images</c>. Se o EF deixar de achar esse campo, nada quebra na compilação —
/// só quando a primeira requisição tentar montar o modelo, em produção. Estes
/// testes constroem o modelo em memória e cobrem essa distância.
/// </summary>
public sealed class RentableItemGalleryMappingTests
{
    private static TenantDbContext BuildContext()
    {
        // Só o modelo é construído; nenhuma conexão é aberta com esta connection string.
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql("Host=localhost;Database=modelo_em_memoria")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenantDbContext(options);
    }

    [Fact]
    public void Model_MapsRentableItemImagesTable()
    {
        using var context = BuildContext();

        var entity = context.Model.FindEntityType(typeof(RentableItemImage));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("rentable_item_images");
    }

    [Fact]
    public void Model_GalleryNavigationReadsTheBackingField()
    {
        using var context = BuildContext();

        var navigation = context.Model
            .FindEntityType(typeof(RentableItem))!
            .FindNavigation(nameof(RentableItem.Images));

        navigation.Should().NotBeNull();
        navigation!.GetPropertyAccessMode().Should().Be(PropertyAccessMode.Field);
        navigation.FieldInfo!.Name.Should().Be("_images");
    }

    [Fact]
    public void Model_DeletingItemCascadesToItsPhotos()
    {
        using var context = BuildContext();

        var foreignKey = context.Model
            .FindEntityType(typeof(RentableItemImage))!
            .GetForeignKeys()
            .Single();

        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        foreignKey.PrincipalEntityType.ClrType.Should().Be<RentableItem>();
    }

    [Fact]
    public void Model_MapsServiceImageUrl()
    {
        using var context = BuildContext();

        var property = context.Model
            .FindEntityType(typeof(Horafy.Domain.Entities.Services.Service))!
            .FindProperty("ImageUrl");

        property.Should().NotBeNull();
        property!.GetColumnName().Should().Be("image_url");
        property!.GetMaxLength().Should().Be(2000);
    }
}
