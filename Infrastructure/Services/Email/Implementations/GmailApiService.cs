using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using InkPulse.Worker.Infrastructure.Services.Email.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Email.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InkPulse.Worker.Infrastructure.Services.Email.Implementations
{
    public class GmailApiService : IGmailApiService
    {
        private readonly GmailApiOptions _options;
        private readonly ILogger<GmailApiService> _logger;

        public GmailApiService(
            IOptions<GmailApiOptions> options, 
            ILogger<GmailApiService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        private GmailService CreateGmailService()
        {
            var clientSecrets = new ClientSecrets
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret
            };

            var tokenResponse = new TokenResponse
            {
                RefreshToken = _options.RefreshToken
            };

            var credential = new UserCredential(
                new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = clientSecrets,
                    Scopes = new[] { GmailService.Scope.GmailSend }
                }),
                "user",
                tokenResponse
            );

            return new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "InkPulse"
            });
        }

        public async Task SendTemplateEmailViaGmailApiAsync(
            string toEmail, 
            string subject, 
            string templateName, 
            Dictionary<string, string> placeholders, 
            CancellationToken cancellationToken = default)
        {
            var templatePath = Path.Combine(AppContext.BaseDirectory, "Emails", templateName);
            if (!File.Exists(templatePath))
            {
                _logger.LogWarning("Email template not found: {TemplatePath}. Using fallback layout.", templatePath);
                throw new FileNotFoundException($"Email template not found: {templateName}", templatePath);
            }

            var emailBody = await File.ReadAllTextAsync(templatePath, cancellationToken);

            emailBody = placeholders.Aggregate(emailBody, (current, placeholder)
                => current.Replace(placeholder.Key, placeholder.Value));

            var subjectEncoded = "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(subject)) + "?=";
            var senderName = _options.SenderName;
            var senderEmail = _options.SenderEmail;
            var from = !string.IsNullOrEmpty(senderName)
                ? $"{senderName} <{senderEmail}>"
                : senderEmail;

            var rawMessageBuilder = new StringBuilder();
            rawMessageBuilder.AppendLine($"From: {from}");
            rawMessageBuilder.AppendLine($"To: {toEmail}");
            rawMessageBuilder.AppendLine($"Subject: {subjectEncoded}");
            rawMessageBuilder.AppendLine("MIME-Version: 1.0");
            rawMessageBuilder.AppendLine("Content-Type: text/html; charset=utf-8");
            rawMessageBuilder.AppendLine();
            rawMessageBuilder.AppendLine(emailBody);

            var rawBytes = Encoding.UTF8.GetBytes(rawMessageBuilder.ToString());
            var rawBase64 = Convert.ToBase64String(rawBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            using var service = CreateGmailService();
            var gmailMessage = new Message
            {
                Raw = rawBase64
            };

            try
            {
                await service.Users.Messages.Send(gmailMessage, "me").ExecuteAsync(cancellationToken);
                _logger.LogInformation("Template email '{TemplateName}' sent successfully to {Email}", templateName, toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send template email '{TemplateName}' to {Email}", templateName, toEmail);
                throw;
            }
        }
    }
}
