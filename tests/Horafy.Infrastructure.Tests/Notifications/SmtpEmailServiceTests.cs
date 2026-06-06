using FluentAssertions;
using Horafy.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Horafy.Infrastructure.Tests.Notifications;

public sealed class SmtpEmailServiceTests
{
    [Fact]
    public void Constructor_ValidOptions_DoesNotThrow()
    {
        var opts = Options.Create(new SmtpOptions
        {
            Host = "smtp.example.com", Port = 587,
            FromAddress = "no-reply@horafy.com.br", FromName = "Horafy"
        });
        var act = () => new SmtpEmailService(opts, NullLogger<SmtpEmailService>.Instance);
        act.Should().NotThrow();
    }

    [Fact]
    public void SmtpOptions_DefaultValues_AreCorrect()
    {
        var opts = new SmtpOptions();
        opts.Port.Should().Be(587);
        opts.FromName.Should().Be("Horafy");
        opts.UseSsl.Should().BeTrue();
    }
}
