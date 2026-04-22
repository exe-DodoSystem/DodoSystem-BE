using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Config;

namespace SMEFLOWSystem.Infrastructure.Services;

public class EmailService : IEmailService
{
    private static readonly TimeSpan SmtpTimeout = TimeSpan.FromSeconds(30);

    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _settings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.FromName))
            throw new InvalidOperationException("Missing config: EmailSettings:FromName");
        if (string.IsNullOrWhiteSpace(_settings.FromEmail))
            throw new InvalidOperationException("Missing config: EmailSettings:FromEmail");
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
            throw new InvalidOperationException("Missing config: EmailSettings:SmtpHost");
        if (_settings.SmtpPort <= 0)
            throw new InvalidOperationException("Missing/invalid config: EmailSettings:SmtpPort");
        if (string.IsNullOrWhiteSpace(_settings.SmtpUsername))
            throw new InvalidOperationException("Missing config: EmailSettings:SmtpUsername");
        if (string.IsNullOrWhiteSpace(_settings.SmtpPassword))
            throw new InvalidOperationException("Missing config: EmailSettings:SmtpPassword");

        await SendSmtpEmailAsync(toEmail, subject, body, cancellationToken, "regular email");
    }

    public async Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.FromName))
            throw new InvalidOperationException("Missing config: EmailSettings:FromName");
        if (string.IsNullOrWhiteSpace(_settings.FromEmail))
            throw new InvalidOperationException("Missing config: EmailSettings:FromEmail");
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
            throw new InvalidOperationException("Missing config: EmailSettings:SmtpHost");
        if (_settings.SmtpPort <= 0)
            throw new InvalidOperationException("Missing/invalid config: EmailSettings:SmtpPort");
        if (string.IsNullOrWhiteSpace(_settings.SmtpUsername))
            throw new InvalidOperationException("Missing config: EmailSettings:SmtpUsername");
        if (string.IsNullOrWhiteSpace(_settings.SmtpPassword))
            throw new InvalidOperationException("Missing config: EmailSettings:SmtpPassword");

        var subject = "SMEFLOW System - Mã OTP của bạn";
        var textBody = $"Mã OTP của bạn là: {otp}\n" +
                       "Mã này có hiệu lực trong 5 phút.\n" +
                       "Nếu bạn không yêu cầu, vui lòng bỏ qua email này.";

        var htmlBody = $@"<p>Mã OTP của bạn là: <strong>{otp}</strong></p>
                       <p>Mã này có hiệu lực trong 5 phút.</p>
                       <p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>";

        var body = string.IsNullOrWhiteSpace(htmlBody) ? textBody : htmlBody;
        await SendSmtpEmailAsync(toEmail, subject, body, cancellationToken, "otp email");
    }

    private async Task SendSmtpEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken, string emailType)
    {
        var plainText = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]+>", "").Trim();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            TextBody = plainText,
            HtmlBody = body
        }.ToMessageBody();

        using var timeoutCts = new CancellationTokenSource(SmtpTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var socketOptions = _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

        _logger.LogInformation(
            "Sending {EmailType} via SMTP to {ToEmail}, host: {Host}:{Port}, from: {FromEmail}",
            emailType,
            toEmail,
            _settings.SmtpHost,
            _settings.SmtpPort,
            _settings.FromEmail);

        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions, linkedCts.Token);
            await smtp.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, linkedCts.Token);
            await smtp.SendAsync(message, linkedCts.Token);
            await smtp.DisconnectAsync(true, linkedCts.Token);

            _logger.LogInformation("SMTP send success: {EmailType} to {ToEmail}", emailType, toEmail);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            _logger.LogError(ex, "SMTP timeout after {TimeoutSeconds}s while sending {EmailType}", SmtpTimeout.TotalSeconds, emailType);
            throw new TimeoutException($"SMTP timeout after {SmtpTimeout.TotalSeconds}s while sending {emailType}.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed for {EmailType} to {ToEmail}", emailType, toEmail);
            throw new InvalidOperationException($"SMTP send failed: {ex.Message}", ex);
        }
    }
}
