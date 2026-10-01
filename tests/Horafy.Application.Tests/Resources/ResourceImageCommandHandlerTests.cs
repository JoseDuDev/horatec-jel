using FluentAssertions;
using Horafy.Application.Common.Images;
using Horafy.Application.Features.Resources;
using Horafy.Application.Features.Resources.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Resources;

public sealed class ResourceImageCommandHandlerTests
{
    private sealed class Harness
    {
        public Mock<IResourceRepository> Resources = new();
        public Mock<IImageStorage>       Storage   = new();
        public Mock<ITenantUnitOfWork>   Uow       = new();

        public Harness(Resource? resource)
        {
            Resources.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(resource);
            Uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Storage.Setup(s => s.SaveAsync(
                        It.IsAny<ImageUpload>(), It.IsAny<ImageFormat>(), "recursos",
                        It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new StoredImage("/media/t/recursos/nova.jpg", "t/recursos/nova.jpg"));
        }

        public SetResourceImageCommandHandler    BuildSet()    => new(Resources.Object, Storage.Object, Uow.Object);
        public RemoveResourceImageCommandHandler BuildRemove() => new(Resources.Object, Storage.Object, Uow.Object);
    }

    private static Resource NewResource(string? avatarUrl = null) =>
        Resource.Create("Ana", ResourceType.Professional, avatarUrl: avatarUrl);

    private static ImageUpload JpegUpload()
    {
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
        return new ImageUpload(new MemoryStream(bytes), "foto.jpg", "image/jpeg", bytes.Length);
    }

    [Fact]
    public async Task Set_StoresPhotoAndReplacesPreviousFile()
    {
        var resource = NewResource("/media/t/recursos/antiga.jpg");
        var harness  = new Harness(resource);

        var result = await harness.BuildSet()
            .Handle(new SetResourceImageCommand(resource.Id, JpegUpload()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("/media/t/recursos/nova.jpg");
        resource.AvatarUrl.Should().Be("/media/t/recursos/nova.jpg");
        harness.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.Storage.Verify(
            s => s.DeleteAsync("/media/t/recursos/antiga.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Set_UnknownResource_ReturnsNotFoundWithoutTouchingStorage()
    {
        var harness = new Harness(null);

        var result = await harness.BuildSet()
            .Handle(new SetResourceImageCommand(Guid.NewGuid(), JpegUpload()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ResourceErrors.NotFound);
        harness.Storage.Verify(s => s.SaveAsync(
            It.IsAny<ImageUpload>(), It.IsAny<ImageFormat>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Set_InvalidImage_KeepsCurrentPhoto()
    {
        var resource = NewResource("/media/t/recursos/antiga.jpg");
        var harness  = new Harness(resource);
        var notImage = new ImageUpload(
            new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x00, 0x00, 0x00, 0x00]),
            "doc.jpg", "image/jpeg", 12);

        var result = await harness.BuildSet()
            .Handle(new SetResourceImageCommand(resource.Id, notImage), default);

        result.IsFailure.Should().BeTrue();
        resource.AvatarUrl.Should().Be("/media/t/recursos/antiga.jpg");
        harness.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Remove_ClearsPhotoAndDeletesFile()
    {
        var resource = NewResource("/media/t/recursos/antiga.jpg");
        var harness  = new Harness(resource);

        var result = await harness.BuildRemove()
            .Handle(new RemoveResourceImageCommand(resource.Id), default);

        result.IsSuccess.Should().BeTrue();
        resource.AvatarUrl.Should().BeNull();
        harness.Storage.Verify(
            s => s.DeleteAsync("/media/t/recursos/antiga.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Remove_ResourceWithoutPhoto_IsNoOp()
    {
        var resource = NewResource();
        var harness  = new Harness(resource);

        var result = await harness.BuildRemove()
            .Handle(new RemoveResourceImageCommand(resource.Id), default);

        result.IsSuccess.Should().BeTrue();
        harness.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        harness.Storage.Verify(
            s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
