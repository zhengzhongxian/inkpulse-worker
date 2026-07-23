using System.Collections.Generic;
using System.Threading.Tasks;
using InkPulse.Worker.Features.Auth.Messages;
using InkPulse.Worker.Infrastructure.Services.Email.Interfaces;
using MassTransit;
using InkPulse.Worker.Infrastructure.Services.Security.Interfaces;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Auth.Consumers
{
    public class SendOtpEmailConsumer : IConsumer<SendOtpEmailMessage>
    {
        private readonly IGmailApiService _gmailApiService;
        private readonly ICryptographyService _cryptographyService;
        private readonly ILogger<SendOtpEmailConsumer> _logger;

        public SendOtpEmailConsumer(
            IGmailApiService gmailApiService,
            ICryptographyService cryptographyService,
            ILogger<SendOtpEmailConsumer> logger)
        {
            _gmailApiService = gmailApiService;
            _cryptographyService = cryptographyService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SendOtpEmailMessage> context)
        {
            var message = context.Message;
            var plainEmail = _cryptographyService.DecryptAes(message.Email);

            _logger.LogInformation("Consuming SendOtpEmailMessage for {Email}. OTP: {Otp}, Expiry: {Expiry} mins", 
                plainEmail, message.Otp, message.ExpiryMinutes);

            var placeholders = new Dictionary<string, string>
            {
                ["{Name}"] = string.IsNullOrEmpty(message.Name) ? plainEmail : message.Name,
                ["{Otp}"] = message.Otp,
                ["{ExpiryTime}"] = message.ExpiryMinutes.ToString()
            };

            await _gmailApiService.SendTemplateEmailViaGmailApiAsync(
                plainEmail,
                message.Subject,
                "otp-template.html",
                placeholders,
                context.CancellationToken
            );
        }
    }
}
