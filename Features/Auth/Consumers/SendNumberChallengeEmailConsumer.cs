using System.Collections.Generic;
using System.Threading.Tasks;
using InkPulse.Worker.Features.Auth.Messages;
using InkPulse.Worker.Infrastructure.Services.Email.Implementations;
using InkPulse.Worker.Infrastructure.Services.Email.Interfaces;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Auth.Consumers
{
    public class SendNumberChallengeEmailConsumer : IConsumer<SendNumberChallengeEmailMessage>
    {
        private readonly GmailApiService _gmailApiService;
        private readonly ILogger<SendNumberChallengeEmailConsumer> _logger;
        private readonly string _verifyBaseUrl;

        public SendNumberChallengeEmailConsumer(
            GmailApiService gmailApiService, 
            IConfiguration configuration,
            ILogger<SendNumberChallengeEmailConsumer> logger)
        {
            _gmailApiService = gmailApiService;
            _logger = logger;
            _verifyBaseUrl = configuration["Auth:Mfa:VerifyBaseUrl"] 
                             ?? "http://localhost/api/v1/auth/mfa/verify-click";
        }

        public async Task Consume(ConsumeContext<SendNumberChallengeEmailMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming SendNumberChallengeEmailMessage for {Email}. SessionId: {SessionId}, Options count: {OptionsCount}", 
                message.Email, message.SessionId, message.Options?.Count ?? -1);

            if (message.Options == null || message.Options.Count < 3)
            {
                _logger.LogWarning("Message options array is null or has less than 3 options. Skipping email sending.");
                return;
            }

            var link1 = $"<a href=\"{_verifyBaseUrl}?sessionId={message.SessionId}&code={message.Options[0]}\" style=\"text-decoration:none; color:#ffffff; display:block; width:100%; height:100%;\">{message.Options[0]}</a>";
            var link2 = $"<a href=\"{_verifyBaseUrl}?sessionId={message.SessionId}&code={message.Options[1]}\" style=\"text-decoration:none; color:#ffffff; display:block; width:100%; height:100%;\">{message.Options[1]}</a>";
            var link3 = $"<a href=\"{_verifyBaseUrl}?sessionId={message.SessionId}&code={message.Options[2]}\" style=\"text-decoration:none; color:#ffffff; display:block; width:100%; height:100%;\">{message.Options[2]}</a>";

            var placeholders = new Dictionary<string, string>
            {
                ["{Opt1}"] = link1,
                ["{Opt2}"] = link2,
                ["{Opt3}"] = link3,
                ["{ExpiryTime}"] = "3"
            };

            await _gmailApiService.SendTemplateEmailViaGmailApiAsync(
                message.Email,
                message.Subject,
                "number-challenge-template.html",
                placeholders,
                context.CancellationToken
            );
        }
    }
}
