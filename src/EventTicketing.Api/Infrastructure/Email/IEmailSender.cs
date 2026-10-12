namespace EventTicketing.Api.Infrastructure.Email;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string TextBody, IReadOnlyList<EmailAttachment> Attachments);

/// <summary>Outgoing e-mail (MVP: in the API; moves to the Notification service in V3-2, AC-17.5).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
