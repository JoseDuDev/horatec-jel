using FluentAssertions;
using Horafy.Domain.Entities.Rentals;
using Xunit;

namespace Horafy.Domain.Tests.Rentals;

public sealed class RentableItemTests
{
    [Fact]
    public void Create_WithValidData_PopulatesFieldsAndIsActive()
    {
        var item = RentableItem.Create(
            name: "  Furadeira  ",
            quantity: 5,
            dailyRate: 30m,
            securityDeposit: 100m,
            bufferDays: 1,
            description: "  Furadeira de impacto  ",
            category: "  Ferramentas  ",
            imageUrl: "  http://img/furadeira.png  ");

        item.Name.Should().Be("Furadeira");
        item.Quantity.Should().Be(5);
        item.DailyRate.Should().Be(30m);
        item.SecurityDeposit.Should().Be(100m);
        item.BufferDays.Should().Be(1);
        item.Description.Should().Be("Furadeira de impacto");
        item.Category.Should().Be("Ferramentas");
        item.ImageUrl.Should().Be("http://img/furadeira.png");
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithMinimalData_UsesDefaults()
    {
        var item = RentableItem.Create(name: "Pula-pula", quantity: 2, dailyRate: 150m);

        item.SecurityDeposit.Should().Be(0m);
        item.BufferDays.Should().Be(0);
        item.Description.Should().BeNull();
        item.Category.Should().BeNull();
        item.ImageUrl.Should().BeNull();
        item.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name)
    {
        var act = () => RentableItem.Create(name, quantity: 1, dailyRate: 10m);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveQuantity_Throws(int quantity)
    {
        var act = () => RentableItem.Create("Item", quantity, dailyRate: 10m);
        act.Should().Throw<ArgumentException>().WithParameterName("quantity");
    }

    [Fact]
    public void Create_WithNegativeDailyRate_Throws()
    {
        var act = () => RentableItem.Create("Item", quantity: 1, dailyRate: -1m);
        act.Should().Throw<ArgumentException>().WithParameterName("dailyRate");
    }

    [Fact]
    public void Create_WithNegativeSecurityDeposit_Throws()
    {
        var act = () => RentableItem.Create("Item", quantity: 1, dailyRate: 10m, securityDeposit: -5m);
        act.Should().Throw<ArgumentException>().WithParameterName("securityDeposit");
    }

    [Fact]
    public void Update_ChangesFieldsAndSetsUpdatedAt()
    {
        var item = RentableItem.Create("Item", quantity: 1, dailyRate: 10m);

        item.Update(
            name: "Item Atualizado",
            quantity: 3,
            dailyRate: 20m,
            securityDeposit: 50m,
            bufferDays: 2,
            description: "nova",
            category: "cat",
            imageUrl: "http://img/x.png");

        item.Name.Should().Be("Item Atualizado");
        item.Quantity.Should().Be(3);
        item.DailyRate.Should().Be(20m);
        item.SecurityDeposit.Should().Be(50m);
        item.BufferDays.Should().Be(2);
        item.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Update_WithInvalidQuantity_Throws()
    {
        var item = RentableItem.Create("Item", quantity: 1, dailyRate: 10m);

        var act = () => item.Update("Item", quantity: 0, dailyRate: 10m,
            securityDeposit: 0m, bufferDays: 0, description: null, category: null, imageUrl: null);

        act.Should().Throw<ArgumentException>().WithParameterName("quantity");
    }

    [Fact]
    public void DeactivateAndActivate_TogglesIsActive()
    {
        var item = RentableItem.Create("Item", quantity: 1, dailyRate: 10m);

        item.Deactivate();
        item.IsActive.Should().BeFalse();

        item.Activate();
        item.IsActive.Should().BeTrue();
    }
}
