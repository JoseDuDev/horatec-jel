using System.Text;
using FluentAssertions;
using Horafy.Application.Common.Images;
using Xunit;

namespace Horafy.Application.Tests.Images;

public sealed class ImageUploadPolicyTests
{
    private static ImageUpload Upload(byte[] content, string contentType = "image/jpeg", string name = "foto.jpg")
    {
        var stream = new MemoryStream(content);
        return new ImageUpload(stream, name, contentType, content.Length);
    }

    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static byte[] Png()  => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private static byte[] Webp()
    {
        var bytes = Encoding.ASCII.GetBytes("RIFF____WEBP");
        return bytes;
    }

    [Fact]
    public async Task Validate_Jpeg_ReturnsJpgFormat()
    {
        var result = await ImageUploadPolicy.ValidateAsync(Upload(Jpeg()));

        result.IsSuccess.Should().BeTrue();
        result.Value.Extension.Should().Be(".jpg");
        result.Value.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task Validate_Png_ReturnsPngFormat()
    {
        var result = await ImageUploadPolicy.ValidateAsync(Upload(Png(), "image/png", "foto.png"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Extension.Should().Be(".png");
    }

    [Fact]
    public async Task Validate_Webp_ReturnsWebpFormat()
    {
        var result = await ImageUploadPolicy.ValidateAsync(Upload(Webp(), "image/webp", "foto.webp"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Extension.Should().Be(".webp");
    }

    [Fact]
    public async Task Validate_EmptyFile_Fails()
    {
        var result = await ImageUploadPolicy.ValidateAsync(Upload([]));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Image.Empty");
    }

    [Fact]
    public async Task Validate_AboveMaxBytes_Fails()
    {
        var upload = new ImageUpload(
            new MemoryStream(Jpeg()), "grande.jpg", "image/jpeg",
            ImageUploadPolicy.MaxBytes + 1);

        var result = await ImageUploadPolicy.ValidateAsync(upload);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Image.TooLarge");
    }

    /// <summary>
    /// O ponto do sniffing: a pasta de uploads é servida publicamente, então um HTML
    /// com extensão e Content-Type de imagem viraria XSS no nosso domínio.
    /// </summary>
    [Fact]
    public async Task Validate_HtmlDisguisedAsJpeg_Fails()
    {
        var html = Encoding.ASCII.GetBytes("<html><script>alert(1)</script>");

        var result = await ImageUploadPolicy.ValidateAsync(Upload(html));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Image.UnsupportedFormat");
    }

    [Fact]
    public async Task Validate_Svg_Fails()
    {
        var svg = Encoding.ASCII.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");

        var result = await ImageUploadPolicy.ValidateAsync(Upload(svg, "image/svg+xml", "logo.svg"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Image.UnsupportedFormat");
    }

    [Fact]
    public async Task Validate_RewindsStream_SoStorageWritesWholeFile()
    {
        var content = Jpeg();
        var upload  = Upload(content);

        await ImageUploadPolicy.ValidateAsync(upload);

        upload.Content.Position.Should().Be(0);

        using var copy = new MemoryStream();
        await upload.Content.CopyToAsync(copy);
        copy.ToArray().Should().Equal(content);
    }
}
