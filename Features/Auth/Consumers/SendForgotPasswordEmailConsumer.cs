using System.Collections.Generic;
using System.Threading.Tasks;
using InkPulse.Worker.Features.Auth.Messages;
using InkPulse.Worker.Infrastructure.Services.Email.Interfaces;
using MassTransit;
using InkPulse.Worker.Infrastructure.Services.Security.Interfaces;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Auth.Consumers
{
    public class SendForgotPasswordEmailConsumer : IConsumer<SendForgotPasswordEmailMessage>
    {
        private readonly IGmailApiService _gmailApiService;
        private readonly ICryptographyService _cryptographyService;
        private readonly ILogger<SendForgotPasswordEmailConsumer> _logger;

        public SendForgotPasswordEmailConsumer(
            IGmailApiService gmailApiService,
            ICryptographyService cryptographyService,
            ILogger<SendForgotPasswordEmailConsumer> logger)
        {
            _gmailApiService = gmailApiService;
            _cryptographyService = cryptographyService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SendForgotPasswordEmailMessage> context)
        {
            var message = context.Message;
            var plainEmail = _cryptographyService.DecryptAes(message.Email);

            _logger.LogInformation("Consuming SendForgotPasswordEmailMessage for {Email}. Expiry: {Expiry} mins",
                plainEmail, message.ExpiryMinutes);

            var placeholders = new Dictionary<string, string>
            {
                ["{Name}"] = string.IsNullOrEmpty(message.Name) ? plainEmail : message.Name,
                ["{ResetLink}"] = message.ResetLink,
                ["{ExpiryTime}"] = message.ExpiryMinutes.ToString()
            };

            await _gmailApiService.SendTemplateEmailViaGmailApiAsync(
                plainEmail,
                message.Subject,
                "forgot-password-template.html",
                placeholders,
                context.CancellationToken
            );
        }
    }
}
