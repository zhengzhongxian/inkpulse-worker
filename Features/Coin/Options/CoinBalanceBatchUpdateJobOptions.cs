using InkPulse.Worker.Infrastructure.Constants;

namespace InkPulse.Worker.Features.Coin.Options
{
    public class CoinBalanceBatchUpdateJobOptions
    {
        public static string SectionName => KeyConstant.ConfigSections.CoinBalanceBatchUpdateJob;

        public string JobName { get; set; } = "CoinBalanceBatchUpdate";
        public string CronExpression { get; set; } = "0/2 * * * * ?";
        public bool Enabled { get; set; } = true;
    }
}
