using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EventTicketing.Notifications.Email;

/// <summary><c>Smtp:*</c> keys (ISKELET §5.14); the defaults point at a local Mailpit (K-08).</summary>
public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 1025;

    [Required]
    [EmailAddress]
    public string From { get; set; } = "noreply@event-ticketing.local";
}

/// <summary>Plain SMTP without authentication or TLS, which is what Mailpit offers locally.</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    /// <summary>Upper bound for connect and send, so an unreachable server fails the attempt quickly and the retry policy takes over.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(smtp.From));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;

        var body = new BodyBuilder { TextBody = message.TextBody };
        foreach (var attachment in message.Attachments)
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        mime.Body = body.ToMessageBody();

        using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
