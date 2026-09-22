using FluentAssertions;
using Horafy.Application.Common.Images;
using Horafy.Application.Interfaces;
using Horafy.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Horafy.Infrastructure.Tests.Storage;

public sealed class LocalDiskImageStorageTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"horafy-uploads-{Guid.NewGuid():N}");

    private static readonly ImageFormat Jpeg = new(".jpg", "image/jpeg");

    private LocalDiskImageStorage BuildStorage(string slug = "locadora")
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.Slug).Returns(slug);

        var options = Options.Create(new ImageStorageOptions
        {
            RootPath      = _root,
            PublicBaseUrl = "https://api.mjml.com.br",
            RequestPath   = "/uploads",
        });

        return new LocalDiskImageStorage(
            tenant.Object,
            new HttpContextAccessor(),
            options,
            NullLogger<LocalDiskImageStorage>.Instance);
    }

    private static ImageUpload Upload(byte[]? content = null)
    {
        var bytes = content ?? [0xFF, 0xD8, 0xFF, 0xE0];
        return new ImageUpload(new MemoryStream(bytes), "foto.jpg", "image/jpeg", bytes.Length);
    }

    [Fact]
    public async Task SaveAsync_WritesFileUnderTenantFolder()
    {
        var storage = BuildStorage();

        var stored = await storage.SaveAsync(Upload(), Jpeg, "itens");

        stored.Key.Should().StartWith("locadora/itens/");
        stored.Key.Should().EndWith(".jpg");
        File.Exists(Path.Combine(_root, stored.Key.Replace('/', Path.DirectorySeparatorChar)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_KeepsEveryByteOfTheUpload()
    {
        var storage = BuildStorage();
        byte[] content = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5];

        var stored = await storage.SaveAsync(Upload(content), Jpeg, "itens");

        var written = await File.ReadAllBytesAsync(
            Path.Combine(_root, stored.Key.Replace('/', Path.DirectorySeparatorChar)));
        written.Should().Equal(content);
    }

    [Fact]
    public async Task SaveAsync_ReturnsPublicUrlUnderConfiguredBase()
    {
        var storage = BuildStorage();

        var stored = await storage.SaveAsync(Upload(), Jpeg, "servicos");

        stored.Url.Should().Be($"https://api.mjml.com.br/uploads/{stored.Key}");
    }

    [Fact]
    public async Task SaveAsync_NamesFilesUniquely()
    {
        var storage = BuildStorage();

        var first  = await storage.SaveAsync(Upload(), Jpeg, "itens");
        var second = await storage.SaveAsync(Upload(), Jpeg, "itens");

        second.Key.Should().NotBe(first.Key);
    }

    [Fact]
    public async Task SaveAsync_WithoutTenant_Throws()
    {
        var storage = BuildStorage(slug: null!);

        var act = () => storage.SaveAsync(Upload(), Jpeg, "itens");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheFile()
    {
        var storage = BuildStorage();
        var stored  = await storage.SaveAsync(Upload(), Jpeg, "itens");
        var path    = Path.Combine(_root, stored.Key.Replace('/', Path.DirectorySeparatorChar));

        await storage.DeleteAsync(stored.Url);

        File.Exists(path).Should().BeFalse();
    }

    /// <summary>
    /// O delete recebe a URL que está no banco do tenant. Se um tenant conseguisse
    /// passar a URL de outro, apagaria a foto do vizinho — o slug da requisição é
    /// conferido antes de tocar no disco.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_FileOfAnotherTenant_IsIgnored()
    {
        var owner  = BuildStorage("locadora");
        var stored = await owner.SaveAsync(Upload(), Jpeg, "itens");
        var path   = Path.Combine(_root, stored.Key.Replace('/', Path.DirectorySeparatorChar));

        var intruder = BuildStorage("outra-loja");
        await intruder.DeleteAsync(stored.Url);

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_PathTraversal_IsIgnored()
    {
        var storage = BuildStorage();
        var victim  = Path.Combine(_root, "alvo.txt");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(victim, "não me apague");

        await storage.DeleteAsync("https://api.mjml.com.br/uploads/locadora/../alvo.txt");

        File.Exists(victim).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ExternalUrl_IsIgnored()
    {
        var storage = BuildStorage();

        var act = () => storage.DeleteAsync("https://algum-site.com/foto.jpg");

        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
