using System.Net;
using System.Net.Mail;

namespace KmcEvents.Api.Services;

// ============================================================
// EMAIL SERVICE INTERFACE
// Supports normal HTML emails and optional attachments.
// ============================================================

public interface IEmailService
{
    Task SendAsync(
        string to,
        string subject,
        string body,
        byte[]? attachment = null,
        string? attachmentName = null
    );
}

// ============================================================
// EMAIL SERVICE
// Gmail SMTP email sending service.
// ============================================================

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IConfiguration config,
        ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    // ========================================================
    // SEND EMAIL
    // ========================================================

    public async Task SendAsync(string to,string subject,string body,byte[]? attachment = null, string? attachmentName = null)
    {
        var enabled = _config.GetValue<bool>("Smtp:Enabled");

        if (!enabled)
        {
            _logger.LogWarning("SMTP is disabled. Email was not sent to {Email}.",to);
            _logger.LogInformation(
                "EMAIL -> {To} | {Subject} | {Body}",to,subject,body);
            return;
        }

        var host = _config["Smtp:Host"];
        var port = _config.GetValue<int>("Smtp:Port");
        var user = _config["Smtp:User"];
        var password = _config["Smtp:Password"];
        var from = _config["Smtp:From"];
        var fromName = _config["Smtp:FromName"] ?? "KMC Event Management";

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException(
                "SMTP configuration is incomplete. Check appsettings.json."
            );
        }

        using var smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(user, password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000
        };

        using var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        message.To.Add(new MailAddress(to));

        // ====================================================
        // OPTIONAL ATTACHMENT
        // Used for QR ticket PNG files.
        // ====================================================

        MemoryStream? attachmentStream = null;

        if (attachment != null &&
            attachment.Length > 0 &&
            !string.IsNullOrWhiteSpace(attachmentName))
        {
            attachmentStream = new MemoryStream(attachment);

            var mailAttachment = new Attachment(
                attachmentStream,
                attachmentName,
                "image/png"
            );

            message.Attachments.Add(mailAttachment);
        }

        try
        {
            await smtpClient.SendMailAsync(message);

            _logger.LogInformation(
                "Email successfully sent to {Email}.",
                to
            );
        }
        catch (SmtpException ex)
        {
            _logger.LogError(
                ex,
                "SMTP failed while sending email to {Email}. Status: {StatusCode}",
                to,
                ex.StatusCode
            );

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while sending email to {Email}.",
                to
            );

            throw;
        }
        finally
        {
            attachmentStream?.Dispose();
        }
    }
}