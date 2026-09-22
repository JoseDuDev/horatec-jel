using FluentAssertions;
using Horafy.Domain.Entities.Rentals;
using Xunit;

namespace Horafy.Domain.Tests.Rentals;

public sealed class RentableItemGalleryTests
{
    private static RentableItem NewItem() =>
        RentableItem.Create(name: "Furadeira", quantity: 1, dailyRate: 30m);

    [Fact]
    public void AddImage_FirstPhoto_BecomesCover()
    {
        var item = NewItem();

        var image = item.AddImage("https://cdn/1.jpg");

        item.Images.Should().ContainSingle();
        image.SortOrder.Should().Be(0);
        item.ImageUrl.Should().Be("https://cdn/1.jpg");
    }

    [Fact]
    public void AddImage_KeepsInsertionOrderAndCover()
    {
        var item = NewItem();

        item.AddImage("https://cdn/1.jpg");
        item.AddImage("https://cdn/2.jpg");
        item.AddImage("https://cdn/3.jpg");

        item.Images.Select(i => i.Url).Should()
            .ContainInOrder("https://cdn/1.jpg", "https://cdn/2.jpg", "https://cdn/3.jpg");
        item.ImageUrl.Should().Be("https://cdn/1.jpg");
    }

    [Fact]
    public void AddImage_BeyondLimit_Throws()
    {
        var item = NewItem();
        for (var i = 0; i < RentableItem.MaxImages; i++)
            item.AddImage($"https://cdn/{i}.jpg");

        var act = () => item.AddImage("https://cdn/extra.jpg");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{RentableItem.MaxImages}*");
    }

    [Fact]
    public void Create_WithImageUrl_SeedsGalleryWithThatPhoto()
    {
        var item = RentableItem.Create(
            name: "Pula-pula", quantity: 1, dailyRate: 150m,
            imageUrl: "  https://cdn/legado.jpg  ");

        item.Images.Should().ContainSingle()
            .Which.Url.Should().Be("https://cdn/legado.jpg");
        item.ImageUrl.Should().Be("https://cdn/legado.jpg");
    }

    [Fact]
    public void RemoveImage_Cover_PromotesNextAndClosesGaps()
    {
        var item = NewItem();
        var first  = item.AddImage("https://cdn/1.jpg");
        item.AddImage("https://cdn/2.jpg");
        item.AddImage("https://cdn/3.jpg");

        item.RemoveImage(first.Id).Should().BeTrue();

        item.ImageUrl.Should().Be("https://cdn/2.jpg");
        item.Images.Select(i => i.SortOrder).Should().ContainInOrder(0, 1);
    }

    [Fact]
    public void RemoveImage_LastPhoto_ClearsCover()
    {
        var item = NewItem();
        var only = item.AddImage("https://cdn/1.jpg");

        item.RemoveImage(only.Id);

        item.Images.Should().BeEmpty();
        item.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void RemoveImage_UnknownId_ReturnsFalseAndKeepsGallery()
    {
        var item = NewItem();
        item.AddImage("https://cdn/1.jpg");

        item.RemoveImage(Guid.NewGuid()).Should().BeFalse();

        item.Images.Should().ContainSingle();
    }

    [Fact]
    public void SetCoverImage_MovesChosenPhotoToFront()
    {
        var item = NewItem();
        item.AddImage("https://cdn/1.jpg");
        item.AddImage("https://cdn/2.jpg");
        var third = item.AddImage("https://cdn/3.jpg");

        item.SetCoverImage(third.Id).Should().BeTrue();

        item.ImageUrl.Should().Be("https://cdn/3.jpg");
        item.Images.Select(i => i.Url).Should()
            .ContainInOrder("https://cdn/3.jpg", "https://cdn/1.jpg", "https://cdn/2.jpg");
    }

    [Fact]
    public void SetCoverImage_UnknownId_ReturnsFalse()
    {
        var item = NewItem();
        item.AddImage("https://cdn/1.jpg");

        item.SetCoverImage(Guid.NewGuid()).Should().BeFalse();
        item.ImageUrl.Should().Be("https://cdn/1.jpg");
    }

    [Fact]
    public void Update_WithGallery_DoesNotOverwriteCover()
    {
        var item = NewItem();
        item.AddImage("https://cdn/capa.jpg");

        item.Update("Furadeira", 2, 40m, 0m, 0, null, null, imageUrl: "https://cdn/intrusa.jpg");

        item.ImageUrl.Should().Be("https://cdn/capa.jpg");
        item.Images.Should().ContainSingle();
    }

    [Fact]
    public void Update_WithoutGallery_SetsImageUrl()
    {
        var item = NewItem();

        item.Update("Furadeira", 2, 40m, 0m, 0, null, null, imageUrl: "https://cdn/nova.jpg");

        item.ImageUrl.Should().Be("https://cdn/nova.jpg");
    }
}
