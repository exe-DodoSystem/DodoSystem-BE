using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Config;

namespace SMEFLOWSystem.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ISendGridClient _sendGrid;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _settings = emailSettings.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_settings.SendGridApiKey))
            throw new InvalidOperationException("Missing config: EmailSettings:SendGridApiKey");

        _sendGrid = new SendGridClient(_settings.SendGridApiKey);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_settings.FromName))
            throw new InvalidOperationException("Missing config: EmailSettings:FromName");
        if (string.IsNullOrWhiteSpace(_settings.FromEmail))
            throw new InvalidOperationException("Missing config: EmailSettings:FromEmail");

        var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
        var to = new EmailAddress(toEmail);

        // Cần có plainText để tránh spam filter
        var plainText = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]+>", "").Trim();
        var message = MailHelper.CreateSingleEmail(from, to, subject, plainText, body);

        _logger.LogInformation("Sending email to {ToEmail}, subject: {Subject}, from: {FromEmail}",
            toEmail, subject, _settings.FromEmail);

        var response = await _sendGrid.SendEmailAsync(message);
        var details = await response.Body.ReadAsStringAsync();

        _logger.LogInformation("SendGrid response: {StatusCode} — {Details}",
            (int)response.StatusCode, details);

        if ((int)response.StatusCode >= 400)
        {
            throw new InvalidOperationException(
                $"SendGrid send failed: {(int)response.StatusCode} {response.StatusCode}. {details}");
        }
    }

    public async Task SendOtpEmailAsync(string toEmail, string otp)
    {
        if (string.IsNullOrWhiteSpace(_settings.FromName))
            throw new InvalidOperationException("Missing config: EmailSettings:FromName");
        if (string.IsNullOrWhiteSpace(_settings.FromEmail))
            throw new InvalidOperationException("Missing config: EmailSettings:FromEmail");

        var subject = "SMEFLOW System - Mã OTP của bạn";
        var textBody = $"Mã OTP của bạn là: {otp}\n" +
                       "Mã này có hiệu lực trong 5 phút.\n" +
                       "Nếu bạn không yêu cầu, vui lòng bỏ qua email này.";

        var htmlBody = $@"<p>Mã OTP của bạn là: <strong>{otp}</strong></p>
                       <p>Mã này có hiệu lực trong 5 phút.</p>
                       <p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>";

        var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
        var to = new EmailAddress(toEmail);
        var message = MailHelper.CreateSingleEmail(from, to, subject, textBody, htmlBody);

        _logger.LogInformation("Sending OTP email to {ToEmail}", toEmail);

        var response = await _sendGrid.SendEmailAsync(message);
        var details = await response.Body.ReadAsStringAsync();

        _logger.LogInformation("SendGrid OTP response: {StatusCode} — {Details}",
            (int)response.StatusCode, details);

        if ((int)response.StatusCode >= 400)
        {
            throw new InvalidOperationException(
                $"SendGrid send failed: {(int)response.StatusCode} {response.StatusCode}. {details}");
        }
    }
}
