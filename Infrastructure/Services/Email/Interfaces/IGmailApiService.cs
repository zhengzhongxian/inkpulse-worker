using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InkPulse.Worker.Infrastructure.Services.Email.Interfaces
{
    public interface IGmailApiService
    {
        Task SendTemplateEmailViaGmailApiAsync(
            string toEmail,
            string subject,
            string templateName,
            Dictionary<string, string> placeholders,
            CancellationToken cancellationToken = default);
    }
}
