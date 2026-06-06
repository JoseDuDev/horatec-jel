using Horafy.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Horafy.Infrastructure.Email;

internal sealed class SmtpEmailService(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpOptions _opts = options.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body    = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

        logger.LogInformation("Enviando e-mail para {To}: {Subject}", to, subject);

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _opts.Host, _opts.Port,
            _opts.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);

        if (!string.IsNullOrEmpty(_opts.Username))
            await client.AuthenticateAsync(_opts.Username, _opts.Password, ct);

        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
