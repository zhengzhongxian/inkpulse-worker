using InkPulse.Worker.Infrastructure.Constants;

namespace InkPulse.Worker.Infrastructure.Services.Security.Models
{
    public class AesSettings
    {
        public static string SectionName => KeyConstant.ConfigSections.Aes;

        public string Key { get; set; } = string.Empty;
        public string Iv { get; set; } = string.Empty;
    }
}
