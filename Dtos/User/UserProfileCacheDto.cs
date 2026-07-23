using InkPulse.Worker.Infrastructure.Services.Caching.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Caching.Models;
using InkPulse.Worker.Infrastructure.Constants;

namespace InkPulse.Worker.Dtos.User
{
    [CacheSection(KeyConstant.CacheSections.UserProfile)]
    public class UserProfileCacheDto : ICacheable
    {
        public string UserId { get; set; } = "";

        public string CacheId()
        {
            return UserId;
        }
    }
}
