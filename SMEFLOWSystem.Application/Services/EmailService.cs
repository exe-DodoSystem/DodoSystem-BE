using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Config;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ISendGridClient _sendGrid;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _settings = emailSettings.Value;

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
            var message = MailHelper.CreateSingleEmail(
                from,
                to,
                subject,
                plainTextContent: string.Empty,
                htmlContent: body);

            var response = await _sendGrid.SendEmailAsync(message);
            if ((int)response.StatusCode >= 400)
            {
                var details = await response.Body.ReadAsStringAsync();
                throw new InvalidOperationException($"SendGrid send failed: {(int)response.StatusCode} {response.StatusCode}. {details}");
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

            var response = await _sendGrid.SendEmailAsync(message);
            if ((int)response.StatusCode >= 400)
            {
                var details = await response.Body.ReadAsStringAsync();
                throw new InvalidOperationException($"SendGrid send failed: {(int)response.StatusCode} {response.StatusCode}. {details}");
            }
        }
    }
}