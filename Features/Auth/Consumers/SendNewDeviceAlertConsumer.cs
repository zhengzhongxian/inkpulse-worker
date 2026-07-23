using System.Collections.Generic;
using System.Threading.Tasks;
using InkPulse.Worker.Features.Auth.Messages;
using InkPulse.Worker.Infrastructure.Services.Email.Interfaces;
using MassTransit;
using InkPulse.Worker.Infrastructure.Services.Security.Interfaces;

namespace InkPulse.Worker.Features.Auth.Consumers
{
    public class SendNewDeviceAlertConsumer : IConsumer<SendNewDeviceAlertEmailMessage>
    {
        private readonly IGmailApiService _gmailApiService;
        private readonly ICryptographyService _cryptographyService;

        public SendNewDeviceAlertConsumer(
            IGmailApiService gmailApiService,
            ICryptographyService cryptographyService)
        {
            _gmailApiService = gmailApiService;
            _cryptographyService = cryptographyService;
        }

        public async Task Consume(ConsumeContext<SendNewDeviceAlertEmailMessage> context)
        {
            var message = context.Message;
            var plainEmail = _cryptographyService.DecryptAes(message.Email);

            var placeholders = new Dictionary<string, string>
            {
                ["{Device}"] = string.IsNullOrEmpty(message.DeviceName) ? "Unknown" : message.DeviceName,
                ["{IpAddress}"] = string.IsNullOrEmpty(message.IpAddress) ? "Unknown" : message.IpAddress
            };

            await _gmailApiService.SendTemplateEmailViaGmailApiAsync(
                plainEmail,
                "InkPulse - New Device Login Alert",
                "new-device-alert-template.html",
                placeholders,
                context.CancellationToken
            );
        }
    }
}
