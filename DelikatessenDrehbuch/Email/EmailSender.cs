using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace DelikatessenDrehbuch.Email
{
    public class EmailSender : IEmailSender
    {

        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IOptions<EmailSettings> emailSettings, ILogger<EmailSender> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            try
            {
                var smtpClient = new SmtpClient
                {
                    Host = _emailSettings.SmtpServer,
                    Port = _emailSettings.SmtpPort,
                    EnableSsl = _emailSettings.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_emailSettings.Email, _emailSettings.Password)
                };

                var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(_emailSettings.Email);
                mailMessage.Subject = subject;
                mailMessage.Body = htmlMessage;
                mailMessage.IsBodyHtml = true;
                mailMessage.To.Add(new MailAddress(email));


                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (SmtpException ex)
            {
                _logger.LogError("SMTP Fehler: {Message}", ex.Message);
                _logger.LogError("Serverantwort: {Response}", ex.StatusCode);
                throw;
            }

           
        }
    }
}
