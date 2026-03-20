using System.Net;
using System.Net.Mail;
using Application.Common.Interfaces;
using Application.Inventory.Models;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<SmtpOptions> options,
        ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.Host);

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("SMTP delivery is not enabled.");
        }

        if (message.To.Count == 0)
        {
            throw new InvalidOperationException("Email recipient list is empty.");
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName ?? _options.FromAddress),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };

        foreach (var recipient in message.To.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            mail.To.Add(recipient.Trim());
        }

        foreach (var cc in message.Cc.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            mail.CC.Add(cc.Trim());
        }

        foreach (var attachment in message.Attachments)
        {
            var stream = new MemoryStream(attachment.Content, writable: false);
            mail.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        _logger.LogInformation(
            "Sending scheduled inventory email to {RecipientCount} recipients with {AttachmentCount} attachments.",
            mail.To.Count,
            mail.Attachments.Count);

        using var registration = ct.Register(static state => ((SmtpClient)state!).SendAsyncCancel(), client);
        await client.SendMailAsync(mail);
    }
}
