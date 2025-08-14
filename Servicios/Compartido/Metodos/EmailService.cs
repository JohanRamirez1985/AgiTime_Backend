using Compartido.DTOs;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.Metodos
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = false);
    }
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _settings;

        public EmailService(IOptions<SmtpSettings> settings)
        {
            _settings = settings.Value;
        }
        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            if (_settings == null)
                throw new InvalidOperationException("La configuración SMTP (_settings) no fue inicializada.");
            if (string.IsNullOrWhiteSpace(_settings.Host))
                throw new InvalidOperationException("SmtpSettings.Host no está configurado.");
            if (string.IsNullOrWhiteSpace(_settings.User))
                throw new InvalidOperationException("SmtpSettings.User no está configurado.");

            using var smtp = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.User, _settings.Password),
                EnableSsl = _settings.EnableSsl
            };

            var message = new MailMessage
            {
                From = new MailAddress(_settings.User, _settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            message.To.Add(to);

            await smtp.SendMailAsync(message);
        }
    }
}
