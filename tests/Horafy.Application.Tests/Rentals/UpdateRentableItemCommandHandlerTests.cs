using FluentAssertions;
using Horafy.Application.Features.Rentals;
using Horafy.Application.Features.Rentals.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Rentals;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Rentals;

public sealed class UpdateRentableItemCommandHandlerTests
{
    private sealed class Harness
    {
        public Mock<IRentableItemRepository> Items = new();
        public Mock<ITenantUnitOfWork>       Uow   = new();

        public Harness(RentableItem? item)
        {
            Items.Setup(r => r.GetByIdWithImagesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(item);
            Uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        public UpdateRentableItemCommandHandler Build() => new(Items.Object, Uow.Object);
    }

    private static UpdateRentableItemCommand Command(Guid id, bool isActive = true) =>
        new(id, "Furadeira 600W", 4, 45m, 120m, 1, "Com maleta", "Ferramentas", isActive);

    [Fact]
    public async Task Handle_UpdatesFields()
    {
        var item    = RentableItem.Create("Furadeira", 1, 30m);
        var harness = new Harness(item);

        var result = await harness.Build().Handle(Command(item.Id), default);

        result.IsSuccess.Should().BeTrue();
        item.Name.Should().Be("Furadeira 600W");
        item.Quantity.Should().Be(4);
        item.DailyRate.Should().Be(45m);
        item.SecurityDeposit.Should().Be(120m);
        item.BufferDays.Should().Be(1);
        item.Description.Should().Be("Com maleta");
        harness.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// O agregado decide a capa olhando as fotos que tem carregadas. Se o handler
    /// buscar o item sem a galeria, editar o preço apaga a foto do catálogo — por
    /// isso o teste fixa também POR ONDE o item é carregado.
    /// </summary>
    [Fact]
    public async Task Handle_KeepsCoverPhoto()
    {
        var item = RentableItem.Create("Furadeira", 1, 30m);
        item.AddImage("https://cdn/capa.jpg");
        var harness = new Harness(item);

        await harness.Build().Handle(Command(item.Id), default);

        item.ImageUrl.Should().Be("https://cdn/capa.jpg");
        item.Images.Should().ContainSingle();
        harness.Items.Verify(
            r => r.GetByIdWithImagesAsync(item.Id, It.IsAny<CancellationToken>()), Times.Once);
        harness.Items.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeactivatesItem()
    {
        var item    = RentableItem.Create("Furadeira", 1, 30m);
        var harness = new Harness(item);

        await harness.Build().Handle(Command(item.Id, isActive: false), default);

        item.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReactivatesItem()
    {
        var item = RentableItem.Create("Furadeira", 1, 30m);
        item.Deactivate();
        var harness = new Harness(item);

        await harness.Build().Handle(Command(item.Id, isActive: true), default);

        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnknownItem_ReturnsNotFound()
    {
        var harness = new Harness(null);

        var result = await harness.Build().Handle(Command(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RentalErrors.ItemNotFound);
        harness.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
