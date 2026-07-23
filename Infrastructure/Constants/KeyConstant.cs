namespace InkPulse.Worker.Infrastructure.Constants
{
    public static class KeyConstant
    {
        public static class ConfigSections
        {
            public const string GmailApi = "GmailApi";
            public const string Ghn = "GhnSettings";
            public const string Aes = "AesSettings";
            public const string Cache = "Cache";
            public const string CoinBalanceBatchUpdateJob = "CoinBalanceBatchUpdateJob";
            public const string FlashSaleJob = "FlashSaleJob";
            public const string FlashSaleElsSyncJob = "FlashSaleElsSyncJob";
        }

        public static class ConnectionStrings
        {
            public const string DefaultConnection = "ConnectionStrings:DefaultConnection";
        }

        public static class CacheSections
        {
            public const string UserProfile = "redis:user_profile";
            public const string CoinPendingDeltas = "redis:coin_pending_deltas";
            public const string FlashSaleStock = "redis:flashsale_stock";
            public const string FlashSaleBuyers = "redis:flashsale_buyers";
        }
    }
}
