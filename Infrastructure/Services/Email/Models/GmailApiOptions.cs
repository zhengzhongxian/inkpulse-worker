using InkPulse.Worker.Infrastructure.Constants;

namespace InkPulse.Worker.Infrastructure.Services.Email.Models
{
    public class GmailApiOptions
    {
        public static string SectionName => KeyConstant.ConfigSections.GmailApi;

        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
    }
}
